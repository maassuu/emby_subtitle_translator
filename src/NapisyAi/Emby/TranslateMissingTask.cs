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
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Entities;
using NapisyAi.Core;
using NapisyAi.Core.Providers;

namespace NapisyAi.Emby
{
    /// <summary>Scheduled task: translate every movie and episode that has English but no target-language subtitles.</summary>
    public class TranslateMissingTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public TranslateMissingTask(ILibraryManager libraryManager, IMediaEncoder mediaEncoder, IProviderManager providerManager, IFileSystem fileSystem, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaEncoder = mediaEncoder;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _logger = logManager.GetLogger("NapisyAi");
        }

        public string Name => "Przetłumacz brakujące napisy";

        public string Key => "NapisyAiTranslateMissing";

        public string Description => "Tłumaczy przez AI napisy filmów i odcinków, które mają angielskie napisy, a nie mają napisów w języku docelowym.";

        public string Category => "Napisy AI";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Movie", "Episode" },
                IsVirtualItem = false,
                HasPath = true,
                Recursive = true,
            });
            var lang = TranslationService.TargetLanguage;
            var todo = items
                .Select(i => (Item: i, Decision: TranslationService.Decide(i)))
                .Where(x => x.Decision.Status == SourceStatus.Ready && !File.Exists(PluginRules.TargetPath(x.Item.Path, lang)))
                .ToList();
            _logger.Info("Do przetłumaczenia: {0}", todo.Count);

            for (var n = 0; n < todo.Count; n++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (item, decision) = todo[n];
                var itemProgress = new Progress<double>(p => progress.Report(100.0 * (n + p / 100.0) / todo.Count));
                try
                {
                    var cues = await TranslationService.TranslateAsync(item, decision.Source!, _mediaEncoder, itemProgress, cancellationToken).ConfigureAwait(false);
                    var target = PluginRules.TargetPath(item.Path, lang);
                    if (SafeFile.WriteNew(target, Srt.Write(cues)))
                    {
                        _providerManager.QueueRefresh(item.InternalId, new MetadataRefreshOptions(_fileSystem), RefreshPriority.Normal);
                        _logger.Info("Zapisano napisy: {0}", target);
                    }
                    else
                    {
                        _logger.Warn("Plik {0} już istnieje - nie nadpisano", target);
                    }
                }
                catch (DailyQuotaException e)
                {
                    _logger.Warn("Przerwano - {0}. Pozostałe pliki zostaną przetłumaczone przy następnym uruchomieniu.", e.Message);
                    break;
                }
                catch (Exception e) when (e is ProviderAuthException || e is BillingException)
                {
                    _logger.Error("Przerwano - {0}", e.Message);
                    break;
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    _logger.Error("Nie przetłumaczono {0}: {1}", item.Path, e.Message);
                }

                progress.Report(100.0 * (n + 1) / todo.Count);
            }
        }
    }
}
