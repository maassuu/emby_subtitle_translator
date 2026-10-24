using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    /// <summary>Claude through the Anthropic Messages API.</summary>
    public sealed class AnthropicProvider : HttpProvider
    {
        private readonly string _apiKey;
        private readonly string _model;

        public AnthropicProvider(HttpClient http, string apiKey, string model) : base(http)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public override async Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            // Server-side fallback lets another model finish a batch that the first one declines.
            var body = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = 16000,
                system = request.System,
                messages = new[] { new { role = "user", content = request.User } },
                output_config = new
                {
                    effort = "medium",
                    format = new { type = "json_schema", schema = JsonDocument.Parse(request.JsonSchema).RootElement },
                },
                fallbacks = "default",
            });
            var (status, text, response) = await PostAsync("https://api.anthropic.com/v1/messages", body, r =>
            {
                r.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                r.Headers.TryAddWithoutValidation("anthropic-version", "2026-10-21");
                r.Headers.TryAddWithoutValidation("anthropic-beta", "server-side-fallback-2026-07-01");
            }, cancellationToken).ConfigureAwait(false);

            var code = (int)status;
            if (code == 401 || code == 403)
            {
                throw new ProviderAuthException("Claude odrzucił klucz API: " + ErrorMessage(text));
            }

            if (code == 429)
            {
                throw new TransientException(RetryAfter(response), "Claude: limit zapytań");
            }

            if (code >= 500)
            {
                throw new TransientException(null, "Claude: " + ErrorMessage(text));
            }

            if (status != HttpStatusCode.OK)
            {
                throw new ProviderException("Claude: " + ErrorMessage(text));
            }

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
            {
                throw new ProviderException("Model odmówił przetłumaczenia tego fragmentu");
            }

            var result = new StringBuilder();
            foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text")
                {
                    result.Append(block.GetProperty("text").GetString());
                }
            }

            return result.ToString();
        }
    }
}
