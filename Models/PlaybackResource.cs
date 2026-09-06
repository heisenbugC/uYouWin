using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Models
{
    internal class PlaybackResource
    {
        public string Url { get; set; }

        public string Container { get; set; }

        public string VideoCodec { get; set; }

        public string AudioCodec { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int AudioBitrateKbps { get; set; }

        public string VideoFormatId { get; set; }

        public string AudioFormatId { get; set; }

        public bool IsMuxed { get; set; }
    }
}
