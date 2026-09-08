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

        public YtDlpVideoInfo()
        {
            Formats = new List<YtDlpFormat>();
        }
    }

    internal sealed class YtDlpFormat
    {
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

        public int? AudioChannels { get; set; }

        public string Language { get; set; }

        public int? LanguagePreference { get; set; }

        public string FormatNote { get; set; }

        public bool? HasDrm { get; set; }
    }
}
