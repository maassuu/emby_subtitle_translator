using NapisyAi.Core;
using Xunit;

namespace NapisyAi.Tests
{
    public class LanguagesTests
    {
        [Theory]
        [InlineData("pl", "pl")]
        [InlineData("POL", "pl")]
        [InlineData("pl-PL", "pl")]
        [InlineData("en_US", "en")]
        [InlineData("ger", "de")]
        [InlineData("deu", "de")]
        [InlineData("und", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Normalizes(string? input, string? expected)
        {
            Assert.Equal(expected, Languages.Normalize(input));
        }

        [Fact]
        public void HasNames()
        {
            var pl = Languages.Get("pl");
            Assert.Equal(("pl", "pol", "polski", "Polish"), (pl.Code1, pl.Code3, pl.NamePl, pl.NameEn));
        }
    }

    public class SourceSelectorTests
    {
        private static SubtitleTrack Ext(int i, string? lang, bool forced = false, bool sdh = false) =>
            new SubtitleTrack(i, lang, true, $"/m/E01.{lang}.srt", true, forced, sdh);

        private static SubtitleTrack Emb(int i, string? lang, bool text = true, bool forced = false) =>
            new SubtitleTrack(i, lang, false, null, text, forced, false);

        [Fact]
        public void TargetTrackMeansHasTarget()
        {
            Assert.Equal(SourceStatus.HasTarget, SourceSelector.Choose(new[] { Ext(1, "eng"), Emb(2, "pol", text: false) }, "pl").Status);
        }

        [Fact]
        public void PrefersExternalFullThenSdhThenEmbedded()
        {
            var full = Ext(1, "eng");
            var sdh = Ext(2, "en", sdh: true);
            var emb = Emb(3, "eng");
            Assert.Equal(full, SourceSelector.Choose(new[] { emb, sdh, full }, "pl").Source);
            Assert.Equal(sdh, SourceSelector.Choose(new[] { emb, sdh }, "pl").Source);
            Assert.Equal(emb, SourceSelector.Choose(new[] { emb, Ext(4, "eng", forced: true) }, "pl").Source);
        }

        [Fact]
        public void ForcedOnlyAsLastResortExternalFirst()
        {
            var extForced = Ext(1, "eng", forced: true);
            var embForced = Emb(2, "eng", forced: true);
            var d = SourceSelector.Choose(new[] { embForced, extForced }, "pl");
            Assert.Equal(SourceStatus.Ready, d.Status);
            Assert.Equal(extForced, d.Source);
            Assert.Equal(embForced, SourceSelector.Choose(new[] { embForced }, "pl").Source);
        }

        [Fact]
        public void ImageOnlyAndNoSource()
        {
            Assert.Equal(SourceStatus.ImageOnly, SourceSelector.Choose(new[] { Emb(2, "eng", text: false) }, "pl").Status);
            Assert.Equal(SourceStatus.ImageOnly, SourceSelector.Choose(new[] { Emb(2, null, text: false) }, "pl").Status);
            Assert.Equal(SourceStatus.NoSource, SourceSelector.Choose(new[] { Emb(2, null) }, "pl").Status);
            Assert.Equal(SourceStatus.NoSource, SourceSelector.Choose(new SubtitleTrack[0], "pl").Status);
            Assert.Null(SourceSelector.Choose(new SubtitleTrack[0], "pl").Source);
        }
    }
}
