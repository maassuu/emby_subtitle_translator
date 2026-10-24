using System.Net;
using System.Text;
using System.Text.Json;
using NapisyAi.Configuration;
using NapisyAi.Core.Providers;
using Xunit;

namespace NapisyAi.Tests
{
    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _respond;
        public readonly List<(HttpRequestMessage Request, string Body)> Calls = new();

        public FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) => _respond = respond;

        public static HttpResponseMessage Json(HttpStatusCode code, string json, (string, string)? header = null)
        {
            var r = new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if (header is { } h)
            {
                r.Headers.TryAddWithoutValidation(h.Item1, h.Item2);
            }

            return r;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));
            return _respond(request, body);
        }
    }

    public class ProviderTests
    {
        private static readonly AiRequest Req = new("SYS", "USER", "{\"type\":\"object\"}");

        private static (IAiProvider, FakeHandler) Make(PluginConfiguration cfg, Func<HttpRequestMessage, string, HttpResponseMessage> respond)
        {
            var handler = new FakeHandler(respond);
            return (ProviderFactory.Create(cfg, new HttpClient(handler)), handler);
        }

        private static PluginConfiguration Gemini() => new() { Provider = "gemini", GeminiApiKey = "gk", GeminiModel = "gemini-x" };
        private static PluginConfiguration Claude() => new() { Provider = "anthropic", AnthropicApiKey = "ak", AnthropicModel = "claude-opus-5-5" };
        private static PluginConfiguration OpenAi() => new() { Provider = "openai", OpenAiApiKey = "ok", OpenAiModel = "gpt-x" };

        [Fact]
        public async Task GeminiHappyPath()
        {
            var (p, h) = Make(Gemini(), (_, _) => FakeHandler.Json(HttpStatusCode.OK,
                "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"lines\\\":\"},{\"text\":\"[]}\"}]}}]}"));
            Assert.Equal("{\"lines\":[]}", await p.CompleteAsync(Req, default));
            var (req, body) = h.Calls[0];
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-x:generateContent", req.RequestUri!.ToString());
            Assert.Equal("gk", req.Headers.GetValues("x-goog-api-key").Single());
            Assert.Equal("napisy-ai", req.Headers.UserAgent.ToString());
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("SYS", doc.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("USER", doc.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
            var gen = doc.RootElement.GetProperty("generationConfig");
            Assert.Equal("application/json", gen.GetProperty("responseMimeType").GetString());
            Assert.Equal("object", gen.GetProperty("responseJsonSchema").GetProperty("type").GetString());
        }

        [Fact]
        public async Task GeminiPerMinuteAndDaily429()
        {
            var perMinute = "{\"error\":{\"code\":429,\"message\":\"Quota exceeded\",\"details\":[{\"@type\":\"type.googleapis.com/google.rpc.RetryInfo\",\"retryDelay\":\"37s\"}]}}";
            var (p1, _) = Make(Gemini(), (_, _) => FakeHandler.Json((HttpStatusCode)429, perMinute));
            var ex = await Assert.ThrowsAsync<TransientException>(() => p1.CompleteAsync(Req, default));
            Assert.Equal(TimeSpan.FromSeconds(37), ex.RetryAfter);

            var daily = "{\"error\":{\"code\":429,\"message\":\"Quota exceeded\",\"details\":[{\"violations\":[{\"quotaId\":\"GenerateRequestsPerDayPerProjectPerModel-FreeTier\"}]}]}}";
            var (p2, _) = Make(Gemini(), (_, _) => FakeHandler.Json((HttpStatusCode)429, daily));
            await Assert.ThrowsAsync<DailyQuotaException>(() => p2.CompleteAsync(Req, default));
        }

        [Theory]
        [InlineData(400, "API key not valid. Please pass a valid API key.", typeof(ProviderAuthException))]
        [InlineData(403, "denied", typeof(ProviderAuthException))]
        [InlineData(404, "model not found", typeof(ProviderException))]
        [InlineData(503, "overloaded", typeof(TransientException))]
        public async Task GeminiErrors(int code, string message, Type expected)
        {
            var (p, _) = Make(Gemini(), (_, _) => FakeHandler.Json((HttpStatusCode)code, $"{{\"error\":{{\"code\":{code},\"message\":\"{message}\"}}}}"));
            var ex = await Record.ExceptionAsync(() => p.CompleteAsync(Req, default));
            Assert.IsType(expected, ex);
        }

        [Fact]
        public async Task ClaudeHappyPath()
        {
            var (p, h) = Make(Claude(), (_, _) => FakeHandler.Json(HttpStatusCode.OK,
                "{\"stop_reason\":\"end_turn\",\"content\":[{\"type\":\"fallback\"},{\"type\":\"text\",\"text\":\"{\\\"lines\\\":[]}\"}]}"));
            Assert.Equal("{\"lines\":[]}", await p.CompleteAsync(Req, default));
            var (req, body) = h.Calls[0];
            Assert.Equal("https://api.anthropic.com/v1/messages", req.RequestUri!.ToString());
            Assert.Equal("ak", req.Headers.GetValues("x-api-key").Single());
            Assert.Equal("2026-10-21", req.Headers.GetValues("anthropic-version").Single());
            Assert.Equal("server-side-fallback-2026-07-01", req.Headers.GetValues("anthropic-beta").Single());
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            Assert.Equal("claude-opus-5-5", root.GetProperty("model").GetString());
            Assert.Equal("SYS", root.GetProperty("system").GetString());
            Assert.Equal("USER", root.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal("medium", root.GetProperty("output_config").GetProperty("effort").GetString());
            Assert.Equal("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());
            Assert.Equal("default", root.GetProperty("fallbacks").GetString());
            Assert.False(root.TryGetProperty("temperature", out _));
            Assert.False(root.TryGetProperty("thinking", out _));
        }

        [Fact]
        public async Task ClaudeRefusalAndErrors()
        {
            var (p1, _) = Make(Claude(), (_, _) => FakeHandler.Json(HttpStatusCode.OK, "{\"stop_reason\":\"refusal\",\"content\":[]}"));
            var refusal = await Assert.ThrowsAsync<ProviderException>(() => p1.CompleteAsync(Req, default));
            Assert.Contains("odmówił", refusal.Message);

            var (p2, _) = Make(Claude(), (_, _) => FakeHandler.Json((HttpStatusCode)429, "{\"error\":{\"message\":\"slow\"}}", ("retry-after", "12")));
            Assert.Equal(TimeSpan.FromSeconds(12), (await Assert.ThrowsAsync<TransientException>(() => p2.CompleteAsync(Req, default))).RetryAfter);

            var (p3, _) = Make(Claude(), (_, _) => FakeHandler.Json((HttpStatusCode)401, "{\"error\":{\"message\":\"invalid x-api-key\"}}"));
            await Assert.ThrowsAsync<ProviderAuthException>(() => p3.CompleteAsync(Req, default));

            var (p4, _) = Make(Claude(), (_, _) => FakeHandler.Json((HttpStatusCode)529, "{\"error\":{\"message\":\"overloaded\"}}"));
            await Assert.ThrowsAsync<TransientException>(() => p4.CompleteAsync(Req, default));

            var (p5, _) = Make(Claude(), (_, _) => FakeHandler.Json((HttpStatusCode)400, "{\"error\":{\"message\":\"credit balance too low\"}}"));
            var bad = await Assert.ThrowsAsync<ProviderException>(() => p5.CompleteAsync(Req, default));
            Assert.Contains("credit balance", bad.Message);
        }

        [Fact]
        public async Task OpenAiHappyPathAndCustomBaseUrl()
        {
            var cfg = OpenAi();
            cfg.OpenAiBaseUrl = "http://localhost:11434/v1/";
            var (p, h) = Make(cfg, (_, _) => FakeHandler.Json(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}"));
            Assert.Equal("{}", await p.CompleteAsync(Req, default));
            var (req, body) = h.Calls[0];
            Assert.Equal("http://localhost:11434/v1/chat/completions", req.RequestUri!.ToString());
            Assert.Equal("Bearer ok", req.Headers.Authorization!.ToString());
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("gpt-x", doc.RootElement.GetProperty("model").GetString());
            Assert.Equal("system", doc.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
            Assert.Equal("json_object", doc.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        }

        [Fact]
        public async Task OpenAiQuotaAndRateLimit()
        {
            var (p1, _) = Make(OpenAi(), (_, _) => FakeHandler.Json((HttpStatusCode)429, "{\"error\":{\"code\":\"insufficient_quota\",\"message\":\"quota\"}}"));
            var ex = await Assert.ThrowsAsync<ProviderException>(() => p1.CompleteAsync(Req, default));
            Assert.Equal("Brak środków na koncie dostawcy", ex.Message);
            var (p2, h2) = Make(OpenAi(), (_, _) => FakeHandler.Json((HttpStatusCode)429, "{\"error\":{\"message\":\"slow\"}}", ("retry-after", "3")));
            Assert.Equal(TimeSpan.FromSeconds(3), (await Assert.ThrowsAsync<TransientException>(() => p2.CompleteAsync(Req, default))).RetryAfter);
            Assert.Equal("https://api.openai.com/v1/chat/completions", h2.Calls[0].Request.RequestUri!.ToString());
        }

        [Fact]
        public async Task NetworkErrorIsTransient()
        {
            var (p, _) = Make(Gemini(), (_, _) => throw new HttpRequestException("no route"));
            await Assert.ThrowsAsync<TransientException>(() => p.CompleteAsync(Req, default));
        }

        [Fact]
        public void FactoryValidates()
        {
            var http = new HttpClient();
            Assert.Equal("Brak klucza API dla wybranego dostawcy", Assert.Throws<ProviderException>(() => ProviderFactory.Create(new PluginConfiguration(), http)).Message);
            Assert.IsType<OpenAiCompatProvider>(ProviderFactory.Create(new PluginConfiguration { Provider = "openai", OpenAiBaseUrl = "http://ollama:11434/v1", OpenAiModel = "llama" }, http));
            Assert.Equal("Podaj nazwę modelu", Assert.Throws<ProviderException>(() => ProviderFactory.Create(new PluginConfiguration { Provider = "openai", OpenAiApiKey = "k" }, http)).Message);
        }
    }

    public class RetryTests
    {
        [Fact]
        public async Task UsesRetryAfterThenBackoffThenSucceeds()
        {
            var slept = new List<TimeSpan>();
            var queue = new Queue<object>(new object[] { new TransientException(TimeSpan.FromSeconds(7), "x"), new TransientException(null, "x"), "ok" });
            var result = await Retry.CallAsync(
                _ => queue.Peek() is Exception e ? Task.FromException<string>((Exception)queue.Dequeue()) : Task.FromResult((string)queue.Dequeue()),
                new RateLimiter(60_000, (d, _) => Task.CompletedTask),
                (d, _) => { slept.Add(d); return Task.CompletedTask; },
                default);
            Assert.Equal("ok", result);
            Assert.Equal(new[] { TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(4) }, slept);
        }

        [Fact]
        public async Task FiveTransientsBecomeDailyQuota()
        {
            await Assert.ThrowsAsync<DailyQuotaException>(() => Retry.CallAsync(
                _ => Task.FromException<string>(new TransientException(null, "x")),
                new RateLimiter(60_000, (d, _) => Task.CompletedTask),
                (d, _) => Task.CompletedTask,
                default));
        }

        [Fact]
        public async Task AuthErrorIsNotRetried()
        {
            var calls = 0;
            await Assert.ThrowsAsync<ProviderAuthException>(() => Retry.CallAsync(
                _ => { calls++; return Task.FromException<string>(new ProviderAuthException("zły klucz")); },
                new RateLimiter(60_000, (d, _) => Task.CompletedTask),
                (d, _) => Task.CompletedTask,
                default));
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task RateLimiterSpacesCalls()
        {
            var waits = new List<TimeSpan>();
            var limiter = new RateLimiter(10, (d, _) => { waits.Add(d); return Task.CompletedTask; });
            await limiter.WaitAsync(default);
            await limiter.WaitAsync(default);
            Assert.Single(waits);
            Assert.InRange(waits[0].TotalSeconds, 5.5, 6.0);
        }
    }
}
