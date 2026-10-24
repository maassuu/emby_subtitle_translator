using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    /// <summary>Any OpenAI-compatible chat API: OpenAI, OpenRouter, DeepSeek, local Ollama and others.</summary>
    public sealed class OpenAiCompatProvider : HttpProvider
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public OpenAiCompatProvider(HttpClient http, string apiKey, string baseUrl, string model) : base(http)
        {
            _apiKey = apiKey;
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.TrimEnd('/');
            _model = model;
        }

        public override async Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            var body = JsonSerializer.Serialize(new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = request.System },
                    new { role = "user", content = request.User },
                },
                response_format = new { type = "json_object" },
            });
            var (status, text, response) = await PostAsync(_baseUrl + "/chat/completions", body, r =>
            {
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    r.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
                }
            }, cancellationToken).ConfigureAwait(false);

            var code = (int)status;
            if (code == 429)
            {
                if (text.Contains("insufficient_quota"))
                {
                    throw new BillingException("Brak środków na koncie dostawcy");
                }

                throw new TransientException(RetryAfter(response), "Limit zapytań dostawcy");
            }

            if (code == 401 || code == 403)
            {
                throw new ProviderAuthException("Dostawca odrzucił klucz API: " + ErrorMessage(text));
            }

            if (code >= 500)
            {
                throw new TransientException(null, ErrorMessage(text));
            }

            if (status != HttpStatusCode.OK)
            {
                throw new ProviderException(ErrorMessage(text));
            }

            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
    }
}
