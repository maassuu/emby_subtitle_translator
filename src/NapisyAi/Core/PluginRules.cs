using System;
using System.Globalization;
using System.IO;

namespace NapisyAi.Core
{
    /// <summary>Small decisions of the Emby layer, kept free of Emby types so they can be tested.</summary>
    public static class PluginRules
    {
        /// <summary>
        /// Offer the AI entry only for a manual search in the target language. Emby's automatic downloads
        /// ask for perfect matches; skipping those keeps library scans from starting paid translations.
        /// </summary>
        public static bool ShouldOffer(string? requestLanguage, bool isPerfectMatch, string targetLanguage)
        {
            return !isPerfectMatch && Languages.Normalize(requestLanguage) == targetLanguage;
        }

        /// <summary>Emby may prefix the id we returned with its own key, e.g. "&lt;hash&gt;_pl_&lt;id&gt;".</summary>
        public static long ParseInternalId(string id)
        {
            return long.Parse(id.Substring(id.LastIndexOf('_') + 1), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        public static string TargetPath(string videoPath, string language)
        {
            return Path.Combine(Path.GetDirectoryName(videoPath) ?? "", Path.GetFileNameWithoutExtension(videoPath) + "." + language + ".srt");
        }
    }
}
