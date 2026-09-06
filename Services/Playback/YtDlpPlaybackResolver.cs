using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    internal class YtDlpPlaybackResolver
    {
        private readonly string _ytDlpPath;

        internal YtDlpPlaybackResolver(string ytDlpPath)
        {
            if (string.IsNullOrWhiteSpace(ytDlpPath))
            {
                throw new ArgumentException(
                    "Path to yt-dlp cannot be null or whitespace.", nameof(ytDlpPath));
            }
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

            string url =
                "https://www.youtube.com/watch?v=" +
                Uri.EscapeDataString(video.Id);

            string formatSelector =
                "best[ext=mp4]" +
                "[height<=" + settings.MaxVideoHeight + "]" +
                "[vcodec^=avc1]" +
                "[acodec^=mp4a]";

            string arguments =
                "--no-playlist " +
                "--no-warnings " +
                "--get-url " +
                "--format " +
                QuoteArgument(formatSelector) +
                " " +
                QuoteArgument(url);

            string output =
                await RunProcessAsync(
                    arguments,
                    cancellationToken);

            string resolvedUrl = output.Trim();

            if (string.IsNullOrWhiteSpace(resolvedUrl))
                throw new InvalidOperationException(
                    "yt-dlp did not return a playback URL.");

            return new PlaybackResource
            {
                Url = resolvedUrl,
                Container = "mp4",
                VideoCodec = settings.VideoCodec,
                AudioCodec = settings.AudioCodec,
                Height = settings.MaxVideoHeight,
                IsMuxed = true
            };
        }

        private async Task<string> RunProcessAsync(
            string arguments,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived +=
                    (sender, e) =>
                    {
                        if (e.Data != null)
                            stdout.AppendLine(e.Data);
                    };

                process.ErrorDataReceived +=
                    (sender, e) =>
                    {
                        if (e.Data != null)
                            stderr.AppendLine(e.Data);
                    };

                if (!process.Start())
                    throw new InvalidOperationException(
                        "Failed to start yt-dlp.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (
                    cancellationToken.Register(
                        () =>
                        {
                            try
                            {
                                if (!process.HasExited)
                                    process.Kill();
                            }
                            catch
                            {
                                // Process may already have exited.
                            }
                        }))
                {
                    await Task.Run(
                        () => process.WaitForExit(),
                        cancellationToken);
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "yt-dlp failed: " + stderr);
                }

                return stdout.ToString();
            }
        }

        private string QuoteArgument(string value)
        {
            return "\"" +
                   value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"") +
                   "\"";
        }
    }
}

