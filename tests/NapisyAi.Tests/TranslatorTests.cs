using System.Text.Json;
using NapisyAi.Core;
using NapisyAi.Core.Providers;
using Xunit;

namespace NapisyAi.Tests
{
    public class PromptTests
    {
        private static readonly Dictionary<int, string> Orig = new() { [1] = "Hi", [2] = "Bye" };

        [Fact]
        public void ParsesFencedJson()
        {
            var raw = "Sure!\n```json\n{\"lines\":[{\"id\":1,\"text\":\"Cześć\"},{\"id\":2,\"text\":\"Pa\"}]}\n```";
            Assert.Equal(new Dictionary<int, string> { [1] = "Cześć", [2] = "Pa" }, Prompt.Parse(raw, new[] { 1, 2 }, Orig));
        }

        [Fact]
        public void ParsesJsonWithTextAround()
        {
            var raw = "Here: {\"lines\":[{\"id\":1,\"text\":\"a\"},{\"id\":2,\"text\":\"b\"}]} done";
            Assert.Equal("b", Prompt.Parse(raw, new[] { 1, 2 }, Orig)[2]);
        }

        [Theory]
        [InlineData("{\"lines\":[{\"id\":1,\"text\":\"a\"}]}")]
        [InlineData("{\"lines\":[{\"id\":1,\"text\":\"a\"},{\"id\":3,\"text\":\"b\"}]}")]
        [InlineData("{\"lines\":[{\"id\":1,\"text\":\"a\"},{\"id\":2,\"text\":\"\"}]}")]
        [InlineData("{\"lines\":[{\"id\":1,\"text\":\"a\"},{\"id\":1,\"text\":\"b\"}]}")]
        [InlineData("{\"lines\":\"x\"}")]
        [InlineData("nope")]
        public void RejectsBadResponses(string raw)
        {
            Assert.Throws<InvalidResponseException>(() => Prompt.Parse(raw, new[] { 1, 2 }, Orig));
        }

        [Fact]
        public void BuildsRequestWithoutPaths()
        {
            var req = Prompt.Build("Show A", "pl", new[] { (1, "Hi") }, new[] { ("Yo", "Siema") });
            using var doc = JsonDocument.Parse(req.User);
            Assert.Equal("Show A", doc.RootElement.GetProperty("title").GetString());
            Assert.Equal("Siema", doc.RootElement.GetProperty("context")[0].GetProperty("translation").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("lines")[0].GetProperty("id").GetInt32());
            Assert.StartsWith("You translate film and TV subtitles from English into Polish.", req.System);
            Assert.Contains("\"lines\"", req.JsonSchema);
        }
    }

    public class TranslatorTests
    {
        private static List<Cue> Cues(params string[] texts) =>
            texts.Select((t, i) => new Cue(i * 1000, i * 1000 + 500, t)).ToList();

        private static string Echo(AiRequest req)
        {
            using var doc = JsonDocument.Parse(req.User);
            var lines = doc.RootElement.GetProperty("lines").EnumerateArray()
                .Select(l => new { id = l.GetProperty("id").GetInt32(), text = "PL:" + l.GetProperty("text").GetString() });
            return JsonSerializer.Serialize(new { lines });
        }

        private sealed class Fake
        {
            public readonly List<AiRequest> Requests = new();
            private readonly Queue<Func<AiRequest, string>> _responses;

            public Fake(params Func<AiRequest, string>[] responses) => _responses = new Queue<Func<AiRequest, string>>(responses);

            public Func<AiRequest, string>? Always { get; init; }

            public Task<string> Call(AiRequest req, CancellationToken ct)
            {
                Requests.Add(req);
                return Task.FromResult((Always ?? _responses.Dequeue())(req));
            }
        }

        private static Task<List<string>> Run(List<Cue> cues, Fake fake, int batch = 60, int context = 15) =>
            Translator.TranslateAsync(cues, fake.Call, "Show", "pl", batch, context, null, CancellationToken.None);

        [Fact]
        public async Task BatchesWithContext()
        {
            var fake = new Fake { Always = Echo };
            var result = await Run(Cues("a", "b", "c", "d", "e"), fake, batch: 2, context: 1);
            Assert.Equal(new[] { "PL:a", "PL:b", "PL:c", "PL:d", "PL:e" }, result);
            Assert.Equal(3, fake.Requests.Count);
            using var second = JsonDocument.Parse(fake.Requests[1].User);
            var ctx = second.RootElement.GetProperty("context");
            Assert.Equal(1, ctx.GetArrayLength());
            Assert.Equal("b", ctx[0].GetProperty("source").GetString());
            Assert.Equal("PL:b", ctx[0].GetProperty("translation").GetString());
            Assert.Equal(3, second.RootElement.GetProperty("lines")[0].GetProperty("id").GetInt32());
        }

        [Fact]
        public async Task BlankCuesSkipRequests()
        {
            var fake = new Fake { Always = Echo };
            Assert.Equal(new[] { "PL:a", "", "PL:c" }, await Run(Cues("a", "  ", "c"), fake));
            Assert.Single(fake.Requests);
            var none = new Fake { Always = Echo };
            Assert.Equal(new[] { "", "" }, await Run(Cues("", " "), none));
            Assert.Empty(none.Requests);
        }

        [Fact]
        public async Task InvalidTwiceThenSplitSucceeds()
        {
            string Ok(int[] ids) => JsonSerializer.Serialize(new { lines = ids.Select(i => new { id = i, text = "t" + i }) });
            var fake = new Fake(_ => "bad", _ => "{\"lines\":[]}", _ => Ok(new[] { 1, 2 }), _ => Ok(new[] { 3, 4 }));
            Assert.Equal(new[] { "t1", "t2", "t3", "t4" }, await Run(Cues("a", "b", "c", "d"), fake));
            Assert.Equal(4, fake.Requests.Count);
        }

        [Fact]
        public async Task SplitStillBadThrows()
        {
            var fake = new Fake { Always = _ => "bad" };
            var ex = await Assert.ThrowsAsync<TranslationException>(() => Run(Cues("a", "b", "c", "d"), fake));
            Assert.Contains("kwestii 1-2", ex.Message);
        }

        [Fact]
        public async Task ProviderErrorsPropagate()
        {
            var fake = new Fake { Always = _ => throw new DailyQuotaException("limit") };
            await Assert.ThrowsAsync<DailyQuotaException>(() => Run(Cues("a"), fake));
        }
    }
}
