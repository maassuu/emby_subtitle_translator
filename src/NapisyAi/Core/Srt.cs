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

        private static readonly Regex BlankLines = new Regex(@"\n\s*\n", RegexOptions.Compiled);

        public static List<Cue> Parse(string text)
        {
            text = text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
            var cues = new List<Cue>();
            foreach (var block in BlankLines.Split(text))
            {
                var lines = block.Trim('\n').Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var m = Timing.Match(lines[i]);
                    if (!m.Success)
                    {
                        continue;
                    }

                    var body = string.Join("\n", lines.Skip(i + 1)).Trim();
                    cues.Add(new Cue(Ms(m, 1), Ms(m, 5), body));
                    break;
                }
            }

            return cues;
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
