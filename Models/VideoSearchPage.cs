using System.Collections.Generic;

namespace uYouWin.Models
{
    public class VideoSearchPage
    {
        public List<Video> Videos { get; set; }

        public string NextPageToken { get; set; }

        public VideoSearchPage()
        {
            Videos = new List<Video>();
        }
    }
}
