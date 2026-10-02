using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Services.Playback
{
    internal sealed class YtDlpVideoInfo
    {
        public double? Duration { get; set; }

        public List<YtDlpFormat> Formats { get; set; }

        public Dictionary<string, List<YtDlpSubtitle>> Subtitles { get; set; }

        public YtDlpVideoInfo()
        {
            Formats = new List<YtDlpFormat>();
            Subtitles = new Dictionary<string, List<YtDlpSubtitle>>();
        }
    }

    internal sealed class YtDlpFormat
    {
        [Newtonsoft.Json.JsonProperty("format_id")]
        public string FormatID { get; set; }

        public string Url { get; set; }

        public string Ext { get; set; }

        public string Protocol { get; set; }

        public string Vcodec { get; set; }

        public string Acodec { get; set; }

        public string Container { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public double? Fps { get; set; }

        public double? Tbr { get; set; }

        public double? Vbr { get; set; }

        public double? Abr { get; set; }

        public int? Asr { get; set; }

        [Newtonsoft.Json.JsonProperty("audio_channels")]
        public int? AudioChannels { get; set; }

        public string Language { get; set; }

        [Newtonsoft.Json.JsonProperty("language_preference")]
        public int? LanguagePreference { get; set; }

        [Newtonsoft.Json.JsonProperty("format_note")]
        public string FormatNote { get; set; }

        [Newtonsoft.Json.JsonProperty("has_drm")]
        public bool? HasDrm { get; set; }
    }

    internal sealed class YtDlpSubtitle
    {
        public string Ext { get; set; }

        public string Url { get; set; }
    }
}
