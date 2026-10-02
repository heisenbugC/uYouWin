using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Models
{
    public class PlaybackSettings
    {
        public int MaxVideoHeight { get; set; }

        public int TargetAudioBitrateKbps { get; set; }

        public string VideoCodec { get; set; }

        public string AudioCodec { get; set; }

        public string PreferredAudioLanguage { get; set; }


        public PlaybackSettings()
        {
            MaxVideoHeight = 1080; // Default to 1080p
            TargetAudioBitrateKbps = 128; // Default to 128 kbps
            VideoCodec = "h264"; // Default video codec
            AudioCodec = "aac"; // Default audio codec
            PreferredAudioLanguage = string.Empty;
        }
    }
}
