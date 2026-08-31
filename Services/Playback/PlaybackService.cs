using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Services.Playback
{
    internal class PlaybackService
    {
        private VideoPlayerInterface _videoPlayer;

        public VideoPlayerInterface Player
        {
            get { return _videoPlayer; }
        }

        public void SetPlayer(VideoPlayerInterface player)
        {
            _videoPlayer = player;
        }
    }
}
