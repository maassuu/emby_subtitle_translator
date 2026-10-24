using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using NapisyAi.Core;

namespace NapisyAi.Emby
{
    /// <summary>Offers "Tłumaczenie AI" in Emby's subtitle search and translates on download.</summary>
    public class AiSubtitleProvider : ISubtitleProvider
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILogger _logger;

        public AiSubtitleProvider(ILibraryManager libraryManager, IMediaEncoder mediaEncoder, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaEncoder = mediaEncoder;
            _logger = logManager.GetLogger("NapisyAi");
        }

        public string Name => "Napisy AI";

        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode, VideoContentType.Movie };

        public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            IEnumerable<RemoteSubtitleInfo> none = Array.Empty<RemoteSubtitleInfo>();
            var target = TranslationService.TargetLanguage;
            if (!PluginRules.ShouldOffer(request.Language, request.IsPerfectMatch, target) || string.IsNullOrEmpty(request.MediaPath))
            {
                return Task.FromResult(none);
            }

            var item = _libraryManager.FindByPath(request.MediaPath, false);
            if (item == null || TranslationService.Decide(item).Status != SourceStatus.Ready)
            {
                return Task.FromResult(none);
            }

            var lang = Languages.Get(target);
            IEnumerable<RemoteSubtitleInfo> results = new[]
            {
                new RemoteSubtitleInfo
                {
                    Id = item.InternalId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ProviderName = Name,
                    Name = $"Tłumaczenie AI ({lang.NamePl})",
                    Comment = "Tłumaczenie trwa zwykle 1-2 minuty",
                    Format = "srt",
                    ThreeLetterISOLanguageName = lang.Code3,
                    Language = lang.Code3,
                },
            };
            return Task.FromResult(results);
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var item = _libraryManager.GetItemById(PluginRules.ParseInternalId(id)) ?? throw new InvalidOperationException("Nie znaleziono elementu biblioteki");
            var decision = TranslationService.Decide(item);
            if (decision.Source == null)
            {
                throw new InvalidOperationException(decision.Status == SourceStatus.HasTarget
                    ? "Element ma już napisy w języku docelowym"
                    : "Brak angielskich napisów do tłumaczenia");
            }

            _logger.Info("Tłumaczenie napisów: {0}", item.Path);
            try
            {
                var cues = await TranslationService.TranslateAsync(item, decision.Source, _mediaEncoder, null, cancellationToken).ConfigureAwait(false);
                _logger.Info("Przetłumaczono {0} kwestii: {1}", cues.Count, item.Path);
                return new SubtitleResponse
                {
                    Format = "srt",
                    Language = Languages.Get(TranslationService.TargetLanguage).Code3,
                    IsForced = decision.Source.IsForced,
                    Stream = new MemoryStream(new UTF8Encoding(false).GetBytes(Srt.Write(cues))),
                };
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _logger.Error("Tłumaczenie nie powiodło się ({0}): {1}", item.Path, e.Message);
                throw;
            }
        }
    }
}
