using System.Text;

namespace NapisyAi.Core
{
    /// <summary>Decodes subtitle files: UTF-8 first, otherwise the most plausible of the common legacy encodings.</summary>
    public static class TextDecoder
    {
        private const string Polish = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ";
        private const string IsoReadAsCp1250 = "±¶Ľ";
        private const string Cp1250ReadAsCp1252 = "¹³¿œŸ¥£¯ŒÊÆ";
        private const string Cp1252ReadAsCp1250 = "ŕďĺľťňřěůőűăţŔĎĹĽŤŇŘĚŮŐŰĂŢ";
        private static readonly int[] Fallbacks = { 1250, 28592, 1252 };

        static TextDecoder()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static string Decode(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            }

            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
            }

            var offset = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(data, offset, data.Length - offset);
            }
            catch (DecoderFallbackException)
            {
            }

            string best = "";
            int? bestScore = null;
            foreach (var codePage in Fallbacks)
            {
                var text = Encoding.GetEncoding(codePage).GetString(data, offset, data.Length - offset);
                var score = Score(text);
                if (bestScore == null || score > bestScore)
                {
                    best = text;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int Score(string text)
        {
            var score = 0;
            foreach (var ch in text)
            {
                if (Polish.IndexOf(ch) >= 0)
                {
                    score += 2;
                }
                else if ((ch >= '\u0080' && ch <= '\u009f') || ch == '�' || IsoReadAsCp1250.IndexOf(ch) >= 0 || Cp1250ReadAsCp1252.IndexOf(ch) >= 0)
                {
                    score -= 5;
                }
                else if (Cp1252ReadAsCp1250.IndexOf(ch) >= 0)
                {
                    score -= 3;
                }
            }

            return score;
        }
    }
}
