using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    public sealed class SubtitleCue
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string Text { get; set; }
    }

    public static class SubtitleCueLoader
    {
        private static readonly HttpClient Http = new HttpClient();

        private static readonly Regex Timestamp = new Regex(
            @"(?<start>(?:\d{1,2}:)?\d{2}:\d{2}[\.,]\d{3})\s+-->\s+(?<end>(?:\d{1,2}:)?\d{2}:\d{2}[\.,]\d{3})",
            RegexOptions.Compiled);
        private static int _generation;

        public static List<SubtitleCue> Cues { get; private set; } =
            new List<SubtitleCue>();

        public static string ActiveLanguage { get; private set; }

        public static async Task LoadAsync(SubtitleTrack track)
        {
            int generation = ++_generation;
            Cues = new List<SubtitleCue>();
            ActiveLanguage = track == null ? null : track.Language;

            if (track == null || string.IsNullOrWhiteSpace(track.Url))
                return;

            string body = await Http.GetStringAsync(track.Url);
            if (generation == _generation)
                Cues = Parse(body);
        }

        public static string TextAt(double seconds)
        {
            foreach (SubtitleCue cue in Cues)
            {
                if (seconds >= cue.Start && seconds <= cue.End)
                    return cue.Text;
            }

            return string.Empty;
        }

        public static void Clear()
        {
            ++_generation;
            Cues = new List<SubtitleCue>();
            ActiveLanguage = null;
        }

        private static List<SubtitleCue> Parse(string body)
        {
            var cues = new List<SubtitleCue>();

            if (string.IsNullOrWhiteSpace(body))
                return cues;

            string[] lines = body.Replace("\r\n", "\n").Split('\n');
            double start = -1;
            double end = -1;
            var text = new List<string>();

            foreach (string raw in lines)
            {
                Match match = Timestamp.Match(raw);

                if (match.Success)
                {
                    Flush(cues, start, end, text);
                    start = ParseTimestamp(match.Groups["start"].Value);
                    end = ParseTimestamp(match.Groups["end"].Value);
                    text.Clear();
                    continue;
                }

                if (start < 0)
                    continue;

                if (string.IsNullOrWhiteSpace(raw))
                {
                    Flush(cues, start, end, text);
                    start = -1;
                    continue;
                }

                if (raw.StartsWith("NOTE", StringComparison.Ordinal) ||
                    raw.StartsWith("WEBVTT", StringComparison.Ordinal))
                    continue;

                text.Add(raw.Trim());
            }

            Flush(cues, start, end, text);
            return cues;
        }

        private static void Flush(
            List<SubtitleCue> cues,
            double start,
            double end,
            List<string> text)
        {
            if (start < 0 || text.Count == 0)
                return;

            cues.Add(new SubtitleCue
            {
                Start = start,
                End = end,
                Text = System.Net.WebUtility.HtmlDecode(Regex.Replace(string.Join("\n", text.ToArray()), "<[^>]+>", ""))
            });
        }

        private static double ParseTimestamp(string value)
        {
            value = value.Replace(',', '.');
            if (value.Split(':').Length == 2)
                value = "00:" + value;
            TimeSpan parsed;

            if (TimeSpan.TryParseExact(
                    value,
                    @"hh\:mm\:ss\.fff",
                    CultureInfo.InvariantCulture,
                    out parsed))
                return parsed.TotalSeconds;

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out parsed))
                return parsed.TotalSeconds;

            return 0;
        }
    }
}
