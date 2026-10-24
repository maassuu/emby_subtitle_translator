using System.Text;
using NapisyAi.Core;
using Xunit;

namespace NapisyAi.Tests
{
    public class ReviewFixTests
    {
        [Fact]
        public void WriteDropsBlankLinesInsideCueText()
        {
            var text = Srt.Write(new[] { new Cue(1000, 2000, "Raz\n\n  \nDwa"), new Cue(3000, 4000, "Trzy") });
            var cues = Srt.Parse(text);
            Assert.Equal(new[] { new Cue(1000, 2000, "Raz\nDwa"), new Cue(3000, 4000, "Trzy") }, cues);
        }

        [Fact]
        public void DecodesUtf16LittleEndianWithBom()
        {
            var s = "1\n00:00:01,000 --> 00:00:02,000\nZażółć\n";
            var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(s)).ToArray();
            Assert.Equal(s, TextDecoder.Decode(bytes));
        }

        [Fact]
        public void DecodesUtf16BigEndianWithBom()
        {
            var s = "Zażółć gęślą";
            var bytes = new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes(s)).ToArray();
            Assert.Equal(s, TextDecoder.Decode(bytes));
        }

        [Fact]
        public void MoveAsideAndRestoreWhenNewFileMissing()
        {
            var dir = Directory.CreateTempSubdirectory().FullName;
            var target = Path.Combine(dir, "Film.pl.srt");
            File.WriteAllText(target, "stare");
            var backup = SafeFile.MoveAside(target);
            Assert.NotNull(backup);
            Assert.False(File.Exists(target));
            Assert.False(backup!.EndsWith(".srt"));
            Assert.True(SafeFile.RestoreIfMissing(target, backup));
            Assert.Equal("stare", File.ReadAllText(target));
            Assert.False(File.Exists(backup));
        }

        [Fact]
        public void BackupDroppedWhenNewFileSaved()
        {
            var dir = Directory.CreateTempSubdirectory().FullName;
            var target = Path.Combine(dir, "Film.pl.srt");
            File.WriteAllText(target, "stare");
            var backup = SafeFile.MoveAside(target)!;
            File.WriteAllText(target, "nowe");
            Assert.False(SafeFile.RestoreIfMissing(target, backup));
            Assert.Equal("nowe", File.ReadAllText(target));
            Assert.False(File.Exists(backup));
        }

        [Fact]
        public void MoveAsideWithoutFileReturnsNull()
        {
            var dir = Directory.CreateTempSubdirectory().FullName;
            Assert.Null(SafeFile.MoveAside(Path.Combine(dir, "Film.pl.srt")));
        }
    }
}
