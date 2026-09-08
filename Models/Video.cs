using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Models
{
    public class Video
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public string ThumbnailUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public double Duration { get; set; } 
        public string WebUrl { get; set; }
        public string YtUrl { get; set; }
        // public List<Thumbnail> Thumbnails { get; set; }
    }
}
