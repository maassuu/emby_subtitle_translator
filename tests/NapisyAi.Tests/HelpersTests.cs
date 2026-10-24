using NapisyAi.Core;
using NapisyAi.Core.Providers;
using System.Net;
using Xunit;

namespace NapisyAi.Tests
{
    public class ProcessArgsTests
    {
        [Theory]
        [InlineData(@"\\nas\Filmy\A B.mkv", @"""\\nas\Filmy\A B.mkv""")]
        [InlineData("/media/tv/Show/E01.mkv", "\"/media/tv/Show/E01.mkv\"")]
        [InlineData("say \"hi\".mkv", "\"say \\\"hi\\\".mkv\"")]
        [InlineData(@"C:\dir\", @"""C:\dir\\""")]
        [InlineData(@"a\""b", @"""a\\\""b""")]
        public void QuotesLikeTheDotNetParserExpects(string arg, string expected)
        {
            Assert.Equal(expected, ProcessArgs.Quote(arg));
        }
    }

    public class PluginRulesTests
    {
        [Theory]
        [InlineData("pol", false, true)]
        [InlineData("pl", false, true)]
        [InlineData("POL", false, true)]
        [InlineData("eng", false, false)]
        [InlineData(null, false, false)]
        [InlineData("pol", true, false)]
        public void OffersOnlyManualSearchInTargetLanguage(string? lang, bool perfectMatch, bool expected)
        {
            Assert.Equal(expected, PluginRules.ShouldOffer(lang, perfectMatch, "pl"));
        }

        [Theory]
        [InlineData("7", 7L)]
        [InlineData("aac4e3dc62880723ce2cb7ae169df641_pl_7", 7L)]
        [InlineData("x_pl_12345678901", 12345678901L)]
        public void ParsesInternalId(string id, long expected)
        {
            Assert.Equal(expected, PluginRules.ParseInternalId(id));
        }

        [Fact]
        public void ParseInternalIdRejectsGarbage()
        {
            Assert.Throws<FormatException>(() => PluginRules.ParseInternalId("abc_pl_xyz"));
        }

        [Fact]
        public void TargetPathNextToVideo()
        {
            var video = Path.Combine("media", "Show", "Show.2019.S01E01.[1080p].mkv");
            Assert.Equal(Path.Combine("media", "Show", "Show.2019.S01E01.[1080p].pl.srt"), PluginRules.TargetPath(video, "pl"));
        }
    }

    public class SafeFileTests
    {
        [Fact]
        public void WritesNewFileWithoutTempLeftovers()
        {
            var dir = Directory.CreateTempSubdirectory().FullName;
            var path = Path.Combine(dir, "a.pl.srt");
            Assert.True(SafeFile.WriteNew(path, "Cześć"));
            Assert.Equal("Cześć", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(dir));
            Assert.False(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        }

        [Fact]
        public void NeverOverwritesExistingFile()
        {
            var dir = Directory.CreateTempSubdirectory().FullName;
            var path = Path.Combine(dir, "a.pl.srt");
            File.WriteAllText(path, "ręczne");
            Assert.False(SafeFile.WriteNew(path, "nowe"));
            Assert.Equal("ręczne", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(dir));
        }
    }

    public class BillingTests
    {
        private static readonly AiRequest Req = new("S", "U", "{}");

        private static IAiProvider Make(Configuration.PluginConfiguration cfg, HttpStatusCode code, string body) =>
            ProviderFactory.Create(cfg, new HttpClient(new FakeHandler((_, _) => FakeHandler.Json(code, body))));

        [Fact]
        public async Task GeminiPrepaymentDepleted()
        {
            var p = Make(new() { GeminiApiKey = "k" }, (HttpStatusCode)402,
                "{\"error\":{\"code\":402,\"message\":\"Your prepayment credits are depleted.\",\"status\":\"RESOURCE_EXHAUSTED\"}}");
            var ex = await Assert.ThrowsAsync<BillingException>(() => p.CompleteAsync(Req, default));
            Assert.StartsWith("Brak środków na koncie Gemini", ex.Message);
        }

        [Fact]
        public async Task ClaudeCreditBalance()
        {
            var p = Make(new() { Provider = "anthropic", AnthropicApiKey = "k" }, HttpStatusCode.BadRequest,
                "{\"error\":{\"message\":\"Your credit balance is too low to access the Anthropic API.\"}}");
            await Assert.ThrowsAsync<BillingException>(() => p.CompleteAsync(Req, default));
        }

        [Fact]
        public async Task OpenAiInsufficientQuota()
        {
            var p = Make(new() { Provider = "openai", OpenAiApiKey = "k", OpenAiModel = "m" }, (HttpStatusCode)429,
                "{\"error\":{\"code\":\"insufficient_quota\",\"message\":\"quota\"}}");
            await Assert.ThrowsAsync<BillingException>(() => p.CompleteAsync(Req, default));
        }
    }
}

namespace NapisyAi.Tests
{
    public class ConfigDefaultsTests
    {
        [Fact]
        public void DefaultsSuitFreeGeminiTier()
        {
            var c = new Configuration.PluginConfiguration();
            Assert.Equal(("gemini", "pl", "gemini-3.5-flash-lite", "claude-opus-5-5", 10), (c.Provider, c.TargetLanguage, c.GeminiModel, c.AnthropicModel, c.RequestsPerMinute));
        }
    }
}
