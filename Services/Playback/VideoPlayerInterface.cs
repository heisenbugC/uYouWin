using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uYouWin.Services.Playback
{
    /// <summary>
    /// Two services will implement this interface,
    /// one for the WindowsMediaPlayer and one for ExternalVideoPlayer.
    /// This interface defines the common methods and properties that both services must implement,
    /// allowing the application to switch between different video players seamlessly.
    /// </summary>
    internal interface VideoPlayerInterface
    {
        void Play();
        void Pause();
        void Stop();
        void Seek(TimeSpan position);
        TimeSpan Position { get; }
        TimeSpan Duration { get; }
        bool IsPlaying { get; }
    }
}
