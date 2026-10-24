using System.Collections.Generic;
using System.Linq;

namespace NapisyAi.Core
{
    /// <summary>A subtitle stream as Emby reports it, reduced to what the selection needs.</summary>
    public sealed record SubtitleTrack(int Index, string? Language, bool IsExternal, string? Path, bool IsText, bool IsForced, bool IsHearingImpaired);

    public enum SourceStatus
    {
        Ready,
        HasTarget,
        NoSource,
        ImageOnly,
    }

    public sealed record SourceDecision(SourceStatus Status, SubtitleTrack? Source);

    public static class SourceSelector
    {
        private const string SourceLanguage = "en";

        public static SourceDecision Choose(IReadOnlyList<SubtitleTrack> tracks, string targetLanguage, bool ignoreExistingTarget = false)
        {
            if (!ignoreExistingTarget && tracks.Any(t => Languages.Normalize(t.Language) == targetLanguage))
            {
                return new SourceDecision(SourceStatus.HasTarget, null);
            }

            var english = tracks.Where(t => t.IsText && Languages.Normalize(t.Language) == SourceLanguage).ToList();
            var candidates = english.Where(t => t.IsExternal && !t.IsForced && !t.IsHearingImpaired)
                .Concat(english.Where(t => t.IsExternal && !t.IsForced && t.IsHearingImpaired))
                .Concat(english.Where(t => !t.IsExternal && !t.IsForced))
                .Concat(english.Where(t => t.IsExternal && t.IsForced))
                .Concat(english.Where(t => !t.IsExternal && t.IsForced));
            var source = candidates.FirstOrDefault();
            if (source != null)
            {
                return new SourceDecision(SourceStatus.Ready, source);
            }

            var imageOnly = tracks.Any(t => !t.IsText && (Languages.Normalize(t.Language) ?? SourceLanguage) == SourceLanguage);
            return new SourceDecision(imageOnly ? SourceStatus.ImageOnly : SourceStatus.NoSource, null);
        }
    }
}
