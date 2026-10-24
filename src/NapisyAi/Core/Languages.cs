using System.Collections.Generic;
using System.Linq;

namespace NapisyAi.Core
{
    public sealed record LanguageInfo(string Code1, string Code3, string NamePl, string NameEn, string[] OtherCodes);

    public static class Languages
    {
        public static readonly IReadOnlyList<LanguageInfo> All = new[]
        {
            new LanguageInfo("pl", "pol", "polski", "Polish", new string[0]),
            new LanguageInfo("en", "eng", "angielski", "English", new string[0]),
            new LanguageInfo("de", "ger", "niemiecki", "German", new[] { "deu" }),
            new LanguageInfo("fr", "fre", "francuski", "French", new[] { "fra" }),
            new LanguageInfo("es", "spa", "hiszpański", "Spanish", new string[0]),
            new LanguageInfo("it", "ita", "włoski", "Italian", new string[0]),
            new LanguageInfo("cs", "cze", "czeski", "Czech", new[] { "ces" }),
            new LanguageInfo("sk", "slo", "słowacki", "Slovak", new[] { "slk" }),
            new LanguageInfo("uk", "ukr", "ukraiński", "Ukrainian", new string[0]),
            new LanguageInfo("ru", "rus", "rosyjski", "Russian", new string[0]),
            new LanguageInfo("pt", "por", "portugalski", "Portuguese", new string[0]),
            new LanguageInfo("nl", "dut", "niderlandzki", "Dutch", new[] { "nld" }),
            new LanguageInfo("sv", "swe", "szwedzki", "Swedish", new string[0]),
        };

        private static readonly Dictionary<string, LanguageInfo> ByCode = All
            .SelectMany(l => new[] { l.Code1, l.Code3 }.Concat(l.OtherCodes).Select(c => (c, l)))
            .ToDictionary(x => x.c, x => x.l);

        /// <summary>ISO 639-1 code for "pl", "POL", "pl-PL", "en_US" and similar, or null.</summary>
        public static string? Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var key = code!.Trim().ToLowerInvariant().Replace('_', '-').Split('-')[0];
            return ByCode.TryGetValue(key, out var lang) ? lang.Code1 : null;
        }

        public static LanguageInfo Get(string code)
        {
            return ByCode[Normalize(code) ?? code];
        }
    }
}
