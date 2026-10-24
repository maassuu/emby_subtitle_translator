using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace NapisyAi.Emby
{
    public class AiSubtitleProvider : ISubtitleProvider
    {
        private readonly ILogger _logger;

        public AiSubtitleProvider(ILogManager logManager)
        {
            _logger = logManager.GetLogger("NapisyAi");
        }

        public string Name => "Napisy AI";

        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode, VideoContentType.Movie };

        public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            _logger.Info("Search language={0} path={1}", request.Language, request.MediaPath);
            var lang = (request.Language ?? "").ToLowerInvariant();
            IEnumerable<RemoteSubtitleInfo> results = lang == "pol" || lang == "pl"
                ? new[]
                {
                    new RemoteSubtitleInfo
                    {
                        Id = "spike",
                        ProviderName = Name,
                        Name = "Tłumaczenie AI (polski)",
                        Format = "srt",
                        ThreeLetterISOLanguageName = "pol",
                        Language = "pol",
                    },
                }
                : Array.Empty<RemoteSubtitleInfo>();
            return Task.FromResult(results);
        }

        public Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var srt = "1\n00:00:00,500 --> 00:00:01,500\nCześć - test wtyczki\n";
            return Task.FromResult(new SubtitleResponse
            {
                Format = "srt",
                Language = "pol",
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(srt)),
            });
        }
    }
}
