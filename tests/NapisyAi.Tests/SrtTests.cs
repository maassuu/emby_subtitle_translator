using System.Text;
using NapisyAi.Core;
using Xunit;

namespace NapisyAi.Tests
{
    public class SrtTests
    {
        [Fact]
        public void ParsesMessySrt()
        {
            var raw = "﻿1\r\n00:00:01,000 --> 00:00:02,500\r\n<i>Hello</i>\r\nthere\r\n\r\n\r\n7\r\n00:00:03,000 --> 00:00:04,000\r\nBye";
            Assert.Equal(new[] { new Cue(1000, 2500, "<i>Hello</i>\nthere"), new Cue(3000, 4000, "Bye") }, Srt.Parse(raw));
        }

        [Fact]
        public void ParsesDotMillisShortMillisAndMissingIndex()
        {
            var raw = "00:00:01.000 --> 00:00:02.000\nHi\n\n2\n00:01:00,5 --> 00:01:01,000\nYo\n";
            Assert.Equal(new[] { new Cue(1000, 2000, "Hi"), new Cue(60500, 61000, "Yo") }, Srt.Parse(raw));
        }

        [Fact]
        public void ParsesEmpty()
        {
            Assert.Empty(Srt.Parse(""));
            Assert.Empty(Srt.Parse("\n\n  \n"));
        }

        [Fact]
        public void WritesAndRoundtrips()
        {
            var cues = new[] { new Cue(1000, 2500, "Cześć\n<i>tam</i>"), new Cue(3_723_004, 3_724_000, "Pa") };
            var text = Srt.Write(cues);
            Assert.StartsWith("1\n00:00:01,000 --> 00:00:02,500\n", text);
            Assert.Contains("01:02:03,004 --> 01:02:04,000", text);
            Assert.Equal(cues, Srt.Parse(text));
        }
    }

    public class TextDecoderTests
    {
        private const string Pl = "Zażółć gęślą jaźń. Śródmieście, źrebię.";

        static TextDecoderTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        [Fact]
        public void DecodesUtf8WithBom()
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Pl)).ToArray();
            Assert.Equal(Pl, TextDecoder.Decode(bytes));
        }

        [Theory]
        [InlineData(1250)]
        [InlineData(28592)]
        public void DecodesPolishLegacyEncodings(int codePage)
        {
            Assert.Equal(Pl, TextDecoder.Decode(Encoding.GetEncoding(codePage).GetBytes(Pl)));
        }

        [Fact]
        public void DecodesWesternCp1252()
        {
            var s = "Café déjà vu – naïve";
            Assert.Equal(s, TextDecoder.Decode(Encoding.GetEncoding(1252).GetBytes(s)));
        }
    }
}
