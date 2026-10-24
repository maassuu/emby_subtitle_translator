using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    /// <summary>Shared HTTP plumbing: sends JSON, maps network failures, reads error bodies.</summary>
    public abstract class HttpProvider : IAiProvider
    {
        private readonly HttpClient _http;

        protected HttpProvider(HttpClient http)
        {
            _http = http;
        }

        public abstract Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken);

        protected async Task<(HttpStatusCode Status, string Body, HttpResponseMessage Response)> PostAsync(
            string url, string json, Action<HttpRequestMessage> headers, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("User-Agent", "napisy-ai");
            headers(request);
            try
            {
                var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (response.StatusCode, body, response);
            }
            catch (HttpRequestException e)
            {
                throw new TransientException(null, "Błąd połączenia: " + e.Message);
            }
            catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TransientException(null, "Przekroczony czas odpowiedzi: " + e.Message);
            }
        }

        protected static string ErrorMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString()!;
                    }

                    if (error.TryGetProperty("message", out var message))
                    {
                        return message.GetString() ?? body;
                    }
                }
            }
            catch (JsonException)
            {
            }

            return body.Length > 300 ? body.Substring(0, 300) : body;
        }

        protected static TimeSpan? RetryAfter(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter?.Delta is { } delta)
            {
                return delta;
            }

            if (response.Headers.TryGetValues("retry-after", out var values)
                && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }
    }
}
