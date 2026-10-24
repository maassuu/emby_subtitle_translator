using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NapisyAi.Core.Providers;

namespace NapisyAi.Core
{
    public sealed class InvalidResponseException : Exception
    {
        public InvalidResponseException(string message) : base(message)
        {
        }
    }

    /// <summary>The prompt for one batch of subtitles and validation of the model's answer.</summary>
    public static class Prompt
    {
        public const string ResponseSchema =
            "{\"type\":\"object\",\"properties\":{\"lines\":{\"type\":\"array\",\"items\":{\"type\":\"object\"," +
            "\"properties\":{\"id\":{\"type\":\"integer\"},\"text\":{\"type\":\"string\"}},\"required\":[\"id\",\"text\"]," +
            "\"additionalProperties\":false}}},\"required\":[\"lines\"],\"additionalProperties\":false}";

        private const string SystemTemplate =
            "You translate film and TV subtitles from English into {0}.\n" +
            "Rules:\n" +
            "- Write natural, colloquial {0}, as in professional subtitles.\n" +
            "- Keep each subtitle roughly as long as the original; keep line breaks inside a subtitle where they make sense.\n" +
            "- Keep formatting tags such as <i>, </i>, <b>, </b> exactly as in the source.\n" +
            "- Do not translate personal names, place names or brand names.\n" +
            "- Keep gender, forms of address and terminology consistent with the context lines.\n" +
            "- Translate every line. Return exactly the same ids, one translation per id, nothing else.\n" +
            "Respond only with JSON: {{\"lines\": [{{\"id\": <id>, \"text\": \"<translation>\"}}]}}";

        private static readonly Regex Fence = new Regex("```(?:json)?\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);

        public static AiRequest Build(string title, string targetLanguage, IEnumerable<(int Id, string Text)> lines, IEnumerable<(string Source, string Translation)> context)
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                w.WriteStartObject();
                w.WriteString("title", title);
                w.WriteStartArray("context");
                foreach (var (source, translation) in context)
                {
                    w.WriteStartObject();
                    w.WriteString("source", source);
                    w.WriteString("translation", translation);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteStartArray("lines");
                foreach (var (id, text) in lines)
                {
                    w.WriteStartObject();
                    w.WriteNumber("id", id);
                    w.WriteString("text", text);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            var system = string.Format(SystemTemplate, Languages.Get(targetLanguage).NameEn);
            return new AiRequest(system, Encoding.UTF8.GetString(stream.ToArray()), ResponseSchema);
        }

        public static Dictionary<int, string> Parse(string raw, IReadOnlyCollection<int> expectedIds, IReadOnlyDictionary<int, string> originals)
        {
            using var doc = Load(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("lines", out var lines)
                || lines.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidResponseException("Brak listy 'lines' w odpowiedzi");
            }

            var result = new Dictionary<int, string>();
            foreach (var item in lines.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var idValue)
                    || !item.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidResponseException("Niepoprawny element listy 'lines'");
                }

                if (result.ContainsKey(idValue))
                {
                    throw new InvalidResponseException($"Powtórzony id {idValue}");
                }

                result[idValue] = text.GetString()!;
            }

            if (!result.Keys.OrderBy(k => k).SequenceEqual(expectedIds.OrderBy(k => k)))
            {
                throw new InvalidResponseException("Identyfikatory kwestii nie zgadzają się z wysłanymi");
            }

            foreach (var pair in result)
            {
                if (!string.IsNullOrWhiteSpace(originals[pair.Key]) && string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new InvalidResponseException($"Puste tłumaczenie kwestii {pair.Key}");
                }
            }

            return result;
        }

        private static JsonDocument Load(string raw)
        {
            var candidates = new List<string> { raw.Trim() };
            var fence = Fence.Match(raw);
            if (fence.Success)
            {
                candidates.Add(fence.Groups[1].Value.Trim());
            }

            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                candidates.Add(raw.Substring(start, end - start + 1));
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    return JsonDocument.Parse(candidate);
                }
                catch (JsonException)
                {
                }
            }

            throw new InvalidResponseException("Odpowiedź nie jest poprawnym JSON-em");
        }
    }
}
