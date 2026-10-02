using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Models
{
    public enum PlaybackResourceType
    {
        DirectStreams,
        DashManifest,
        HlsManifest,
        TemporaryFile
    }

    public class PlaybackResource
    {
        public PlaybackResourceType Type { get; set; }

        public string VideoUrl { get; set; }

        public string AudioUrl { get; set; }

        public string VideoContainer { get; set; }

        public string AudioContainer { get; set; }

        public string VideoCodec { get; set; }

        public string AudioCodec { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double Fps { get; set; }

        public double AudioBitrateKbps { get; set; }

        public int AudioSampleRate { get; set; }

        public int AudioChannels { get; set; }

        public string VideoFormatId { get; set; }

        public string AudioFormatId { get; set; }

        public string Language { get; set; }

        public double DurationSeconds { get; set; }

        public List<string> AudioTrackLabels { get; set; }

        public List<SubtitleTrack> SubtitleTracks { get; set; }

        public PlaybackResource()
        {
            AudioTrackLabels = new List<string>();
            SubtitleTracks = new List<SubtitleTrack>();
        }
    }
}
