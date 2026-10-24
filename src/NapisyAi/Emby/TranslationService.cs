using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.MediaEncoding;
using NapisyAi.Configuration;
using NapisyAi.Core;
using NapisyAi.Core.Providers;

namespace NapisyAi.Emby
{
    /// <summary>Translates one item at a time, shared by the subtitle provider and the scheduled task.</summary>
    public static class TranslationService
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private static readonly SemaphoreSlim OneAtATime = new SemaphoreSlim(1, 1);
        private static RateLimiter? _limiter;

        public static PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

        public static string TargetLanguage => Languages.Normalize(Config.TargetLanguage) ?? "pl";

        public static SourceDecision Decide(BaseItem item) => SourceSelector.Choose(SubtitleSource.Tracks(item), TargetLanguage);

        public static async Task<List<Cue>> TranslateAsync(BaseItem item, SubtitleTrack source, IMediaEncoder encoder, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            var config = Config;
            var provider = ProviderFactory.Create(config, Http);
            var cues = await SubtitleSource.LoadAsync(item, source, encoder, cancellationToken).ConfigureAwait(false);
            if (cues.Count == 0)
            {
                throw new TranslationException("Napisy źródłowe są puste");
            }

            await OneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var limiter = Limiter(config.RequestsPerMinute);
                var title = item is Episode episode && !string.IsNullOrEmpty(episode.SeriesName) ? episode.SeriesName : item.Name;
                var texts = await Translator.TranslateAsync(
                    cues,
                    (req, ct) => Retry.CallAsync(c => provider.CompleteAsync(req, c), limiter, null, ct),
                    title,
                    TargetLanguage,
                    config.BatchSize,
                    config.ContextSize,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                return cues.Zip(texts, (c, t) => c with { Text = t }).ToList();
            }
            finally
            {
                OneAtATime.Release();
            }
        }

        private static RateLimiter Limiter(int rpm)
        {
            var interval = TimeSpan.FromSeconds(60.0 / Math.Max(1, rpm));
            if (_limiter == null || _limiter.Interval != interval)
            {
                _limiter = new RateLimiter(rpm);
            }

            return _limiter;
        }
    }
}
