using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NapisyAi.Core.Providers;

namespace NapisyAi.Core
{
    public sealed class TranslationException : Exception
    {
        public TranslationException(string message) : base(message)
        {
        }
    }

    /// <summary>Translates cues batch by batch, giving the model the preceding lines as context.</summary>
    public static class Translator
    {
        public static async Task<List<string>> TranslateAsync(
            IReadOnlyList<Cue> cues,
            Func<AiRequest, CancellationToken, Task<string>> call,
            string title,
            string targetLanguage,
            int batchSize,
            int contextSize,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, string>();
            for (var i = 1; i <= cues.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(cues[i - 1].Text))
                {
                    result[i] = "";
                }
            }

            var todo = Enumerable.Range(1, cues.Count).Where(i => !result.ContainsKey(i)).ToList();
            var size = Math.Max(1, batchSize);
            var batches = Enumerable.Range(0, (todo.Count + size - 1) / size).Select(b => todo.Skip(b * size).Take(size).ToList()).ToList();

            List<(string, string)> ContextFor(int firstId)
            {
                var ctx = new List<(string, string)>();
                for (var i = firstId - 1; i >= 1 && ctx.Count < contextSize; i--)
                {
                    if (result.TryGetValue(i, out var t) && t.Length > 0)
                    {
                        ctx.Add((cues[i - 1].Text, t));
                    }
                }

                ctx.Reverse();
                return ctx;
            }

            async Task<Dictionary<int, string>?> Attempt(List<int> ids)
            {
                var originals = ids.ToDictionary(i => i, i => cues[i - 1].Text);
                var request = Prompt.Build(title, targetLanguage, ids.Select(i => (i, originals[i])), ContextFor(ids[0]));
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        return Prompt.Parse(await call(request, cancellationToken).ConfigureAwait(false), ids, originals);
                    }
                    catch (InvalidResponseException)
                    {
                    }
                }

                return null;
            }

            for (var n = 0; n < batches.Count; n++)
            {
                var ids = batches[n];
                var translated = await Attempt(ids).ConfigureAwait(false);
                if (translated == null)
                {
                    if (ids.Count == 1)
                    {
                        throw Failure(ids);
                    }

                    var half = ids.Count / 2;
                    foreach (var part in new[] { ids.Take(half).ToList(), ids.Skip(half).ToList() })
                    {
                        var partResult = await Attempt(part).ConfigureAwait(false) ?? throw Failure(part);
                        foreach (var pair in partResult)
                        {
                            result[pair.Key] = pair.Value;
                        }
                    }
                }
                else
                {
                    foreach (var pair in translated)
                    {
                        result[pair.Key] = pair.Value;
                    }
                }

                progress?.Report(100.0 * (n + 1) / batches.Count);
            }

            return Enumerable.Range(1, cues.Count).Select(i => result[i]).ToList();
        }

        private static TranslationException Failure(List<int> ids) =>
            new TranslationException($"Model zwrócił niepoprawną odpowiedź dla kwestii {ids[0]}-{ids[ids.Count - 1]}");
    }
}
