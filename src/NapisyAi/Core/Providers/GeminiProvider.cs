using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    /// <summary>Google Gemini REST API (key from Google AI Studio).</summary>
    public sealed class GeminiProvider : HttpProvider
    {
        private static readonly Regex RetryDelay = new Regex("\"retryDelay\"\\s*:\\s*\"(\\d+(?:\\.\\d+)?)s\"", RegexOptions.Compiled);
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiProvider(HttpClient http, string apiKey, string model) : base(http)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public override async Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            var body = JsonSerializer.Serialize(new
            {
                systemInstruction = new { parts = new[] { new { text = request.System } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = request.User } } } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseJsonSchema = JsonDocument.Parse(request.JsonSchema).RootElement,
                },
            });
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent";
            var (status, text, _) = await PostAsync(url, body, r => r.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey), cancellationToken)
                .ConfigureAwait(false);
            if (status != HttpStatusCode.OK)
            {
                throw MapError((int)status, text);
            }

            using var doc = JsonDocument.Parse(text);
            var result = new StringBuilder();
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0
                && candidates[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var t))
                    {
                        result.Append(t.GetString());
                    }
                }
            }

            return result.ToString();
        }

        private static Exception MapError(int code, string body)
        {
            var message = ErrorMessage(body);
            if (code == 429)
            {
                if (body.Contains("PerDay"))
                {
                    return new DailyQuotaException("Wyczerpany dzienny limit Gemini");
                }

                var m = RetryDelay.Match(body);
                return new TransientException(
                    m.Success ? TimeSpan.FromSeconds(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : (TimeSpan?)null,
                    "Gemini: limit zapytań na minutę");
            }

            if (code == 402)
            {
                return new BillingException("Brak środków na koncie Gemini - doładuj projekt w AI Studio albo użyj klucza z projektu na darmowym poziomie");
            }

            if (code == 401 || code == 403 || (code == 400 && message.Contains("API key")))
            {
                return new ProviderAuthException("Gemini odrzucił klucz API: " + message);
            }

            if (code >= 500)
            {
                return new TransientException(null, "Gemini: " + message);
            }

            return new ProviderException("Gemini: " + message);
        }
    }
}
