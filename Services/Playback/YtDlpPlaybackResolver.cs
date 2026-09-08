using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    public sealed class YtDlpPlaybackResolver : PlaybackResolverInterface
    {
        private readonly string _ytDlpPath;

        public YtDlpPlaybackResolver(string ytDlpPath)
        {
            if (string.IsNullOrWhiteSpace(ytDlpPath))
                throw new ArgumentException(
                    "yt-dlp path cannot be empty.",
                    nameof(ytDlpPath));

            _ytDlpPath = ytDlpPath;
        }

        public async Task<PlaybackResource> ResolveAsync(
            Video video,
            PlaybackSettings settings,
            CancellationToken cancellationToken)
        {
            if (video == null)
                throw new ArgumentNullException(nameof(video));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(video.YtUrl))
                throw new ArgumentException(
                    "Video does not have a YouTube URL.",
                    nameof(video));

            string json = await RunYtDlpAsync(
                video.YtUrl,
                cancellationToken);

            YtDlpVideoInfo info;

            try
            {
                info = JsonConvert.DeserializeObject<YtDlpVideoInfo>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "yt-dlp returned invalid JSON.",
                    ex);
            }

            if (info == null)
                throw new InvalidOperationException(
                    "yt-dlp returned no video information.");

            YtDlpFormat videoFormat =
                SelectVideoFormat(info, settings);

            YtDlpFormat audioFormat =
                SelectAudioFormat(info, settings);

            if (videoFormat == null)
            {
                throw new InvalidOperationException(
                    "No compatible H.264 video format is available.");
            }

            if (audioFormat == null)
            {
                throw new InvalidOperationException(
                    "No compatible AAC audio format is available.");
            }

            return new PlaybackResource
            {
                Type = PlaybackResourceType.DirectStreams,

                VideoUrl = videoFormat.Url,
                AudioUrl = audioFormat.Url,

                VideoFormatId = videoFormat.FormatID,
                AudioFormatId = audioFormat.FormatID,

                VideoCodec = videoFormat.Vcodec,
                AudioCodec = audioFormat.Acodec,

                VideoContainer = videoFormat.Container,
                AudioContainer = audioFormat.Container,

                Width = videoFormat.Width ?? 0,
                Height = videoFormat.Height ?? 0,
                Fps = videoFormat.Fps ?? 0,

                AudioBitrateKbps = audioFormat.Abr ?? 0,
                AudioSampleRate = audioFormat.Asr ?? 0,
                AudioChannels = audioFormat.AudioChannels ?? 0,

                Language = audioFormat.Language,

                DurationSeconds = info.Duration ?? 0
            };
        }

        private static YtDlpFormat SelectVideoFormat(
            YtDlpVideoInfo info,
            PlaybackSettings settings)
        {
            return info.Formats
                .Where(IsUsableH264Video)
                .Where(f =>
                    f.Height.HasValue &&
                    f.Height.Value > 0 &&
                    f.Height.Value <= settings.MaxVideoHeight)
                .OrderByDescending(f => f.Height.Value)
                .ThenByDescending(f => f.Fps ?? 0)
                .ThenByDescending(f => f.Tbr ?? 0)
                .FirstOrDefault();
        }

        private static YtDlpFormat SelectAudioFormat(
            YtDlpVideoInfo info,
            PlaybackSettings settings)
        {
            var candidates = info.Formats
                .Where(IsUsableAacAudio)
                .ToList();

            if (candidates.Count == 0)
                return null;

            /*
             * Prefer the original/default language.
             *
             * yt-dlp generally gives language_preference > 0 to
             * preferred/original tracks.
             */
            candidates = candidates
                .OrderByDescending(f => f.LanguagePreference ?? 0)
                .ThenBy(f =>
                {
                    double abr = f.Abr ?? 0;
                    return Math.Abs(
                        abr - settings.TargetAudioBitrateKbps);
                })
                .ThenByDescending(f => f.Abr ?? 0)
                .ToList();

            return candidates[0];
        }

        private static bool IsUsableH264Video(
            YtDlpFormat format)
        {
            if (format == null)
                return false;

            if (string.IsNullOrWhiteSpace(format.Url))
                return false;

            if (string.IsNullOrWhiteSpace(format.Vcodec))
                return false;

            if (!format.Vcodec.StartsWith(
                    "avc1",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(
                    format.Acodec,
                    "none",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(
                    format.Ext,
                    "mp4",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(
                    format.Protocol,
                    "https",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (format.HasDrm == true)
                return false;

            return true;
        }

        private static bool IsUsableAacAudio(
            YtDlpFormat format)
        {
            if (format == null)
                return false;

            if (string.IsNullOrWhiteSpace(format.Url))
                return false;

            if (!string.Equals(
                    format.Vcodec,
                    "none",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(format.Acodec))
                return false;

            if (!format.Acodec.StartsWith(
                    "mp4a",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(
                    format.Ext,
                    "m4a",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(
                    format.Protocol,
                    "https",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (format.HasDrm == true)
                return false;

            return true;
        }

        private async Task<string> RunYtDlpAsync(
            string url,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(_ytDlpPath))
            {
                throw new FileNotFoundException(
                    "yt-dlp executable was not found.",
                    _ytDlpPath);
            }

            string escapedUrl =
                url.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,

                Arguments =
                    "-j --no-playlist --no-warnings \"" +
                    escapedUrl +
                    "\"",

                UseShellExecute = false,
                CreateNoWindow = true,

                RedirectStandardOutput = true,
                RedirectStandardError = true,

                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;

                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Failed to start yt-dlp.");
                }

                Task<string> outputTask =
                    process.StandardOutput.ReadToEndAsync();

                Task<string> errorTask =
                    process.StandardError.ReadToEndAsync();

                try
                {
                    await WaitForExitAsync(
                        process,
                        cancellationToken);
                }
                catch
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch
                    {
                    }

                    throw;
                }

                string output = await outputTask;
                string error = await errorTask;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "yt-dlp failed with exit code " +
                        process.ExitCode +
                        ": " +
                        error);
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new InvalidOperationException(
                        "yt-dlp returned empty output.");
                }

                return output.Trim();
            }
        }

        private static async Task WaitForExitAsync(
            Process process,
            CancellationToken cancellationToken)
        {
            while (!process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(
                    50,
                    cancellationToken);
            }
        }
    }
}