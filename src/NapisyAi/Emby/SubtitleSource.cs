using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using NapisyAi.Core;

namespace NapisyAi.Emby
{
    /// <summary>Reads the subtitle streams Emby knows about and loads the chosen one as cues.</summary>
    public static class SubtitleSource
    {
        public static List<SubtitleTrack> Tracks(BaseItem item)
        {
            return item.GetMediaStreams()
                .Where(s => s.Type == MediaStreamType.Subtitle)
                .Select(s => new SubtitleTrack(s.Index, s.Language, s.IsExternal, s.Path, s.IsTextSubtitleStream, s.IsForced, s.IsHearingImpaired))
                .ToList();
        }

        public static async Task<List<Cue>> LoadAsync(BaseItem item, SubtitleTrack track, IMediaEncoder encoder, CancellationToken cancellationToken)
        {
            if (track.IsExternal && track.Path != null && track.Path.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
            {
                return Srt.Parse(TextDecoder.Decode(File.ReadAllBytes(track.Path)));
            }

            // Embedded tracks and other external formats (ass, vtt) are converted to SRT with Emby's ffmpeg.
            var temp = Path.Combine(Path.GetTempPath(), "napisyai-" + Guid.NewGuid().ToString("N") + ".srt");
            try
            {
                var args = track.IsExternal
                    ? new[] { "-v", "error", "-y", "-i", track.Path!, "-c:s", "srt", temp }
                    : new[] { "-v", "error", "-y", "-i", item.Path, "-map", "0:" + track.Index, "-c:s", "srt", temp };
                await RunAsync(encoder.EncoderPath, args, cancellationToken).ConfigureAwait(false);
                return Srt.Parse(TextDecoder.Decode(File.ReadAllBytes(temp)));
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private static async Task RunAsync(string exe, IEnumerable<string> args, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                Arguments = string.Join(" ", args.Select(Quote)),
            };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Nie udało się uruchomić ffmpeg");
            var stderr = process.StandardError.ReadToEndAsync();
            _ = process.StandardOutput.ReadToEndAsync();
            using (cancellationToken.Register(() => { try { process.Kill(); } catch (InvalidOperationException) { } }))
            {
                await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
            }

            if (process.ExitCode != 0)
            {
                var err = await stderr.ConfigureAwait(false);
                throw new InvalidOperationException("ffmpeg nie wyciągnął napisów: " + (err.Length > 300 ? err.Substring(0, 300) : err));
            }
        }

        private static string Quote(string arg) => "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
