using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Models
{
    public class Channel
    {
        public int Id { get; set; }

        public string ChannelId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string ThumbnailUrl { get; set; }

        public string WebUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string Country { get; set; }
        public string CustomUrl { get; set; }
        public long? SubscriberCount { get; set; }
        public long? VideoCount { get; set; }
        public long? ViewCount { get; set; }
        public bool HiddenSubscriberCount { get; set; }
        public string UploadsPlaylistId { get; set; }
        public DateTime? MetadataFetchedAtUtc { get; set; }
    }
}
