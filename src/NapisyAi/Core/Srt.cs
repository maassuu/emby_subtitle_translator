using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NapisyAi.Core
{
    public static class Srt
    {
        private static readonly Regex Timing = new Regex(
            @"^\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})",
            RegexOptions.Compiled);

        public static List<Cue> Parse(string text)
        {
            var lines = text.TrimStart('\ufeff').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var timings = new List<int>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (Timing.IsMatch(lines[i]))
                {
                    timings.Add(i);
                }
            }

            var cues = new List<Cue>();
            for (var t = 0; t < timings.Count; t++)
            {
                var start = timings[t] + 1;
                var end = t + 1 < timings.Count ? timings[t + 1] : lines.Length;
                var body = lines.Skip(start).Take(end - start).ToList();
                // The cue number of the next subtitle sits right before its timing line.
                if (t + 1 < timings.Count && body.Count > 0 && IsIndex(body[body.Count - 1]))
                {
                    body.RemoveAt(body.Count - 1);
                }

                var m = Timing.Match(lines[timings[t]]);
                cues.Add(new Cue(Ms(m, 1), Ms(m, 5), string.Join("\n", body).Trim()));
            }

            return cues;
        }

        private static bool IsIndex(string line)
        {
            var trimmed = line.Trim();
            return trimmed.Length > 0 && trimmed.All(char.IsDigit);
        }

        public static string Write(IReadOnlyList<Cue> cues)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < cues.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(i + 1).Append('\n')
                    .Append(Format(cues[i].StartMs)).Append(" --> ").Append(Format(cues[i].EndMs)).Append('\n')
                    .Append(cues[i].Text).Append('\n');
            }

            return sb.ToString();
        }

        private static long Ms(Match m, int first)
        {
            long Part(int i) => long.Parse(m.Groups[first + i].Value, CultureInfo.InvariantCulture);
            var frac = long.Parse(m.Groups[first + 3].Value.PadRight(3, '0'), CultureInfo.InvariantCulture);
            return ((Part(0) * 60 + Part(1)) * 60 + Part(2)) * 1000 + frac;
        }

        private static string Format(long ms)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00},{3:000}",
                ms / 3_600_000,
                ms / 60_000 % 60,
                ms / 1000 % 60,
                ms % 1000);
        }
    }
}
