using System;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    /// <summary>
    /// Two services will implement this interface,
    /// one for the WindowsMediaPlayer and one for ExternalVideoPlayer.
    /// This interface defines the common methods and properties that both services must implement,
    /// allowing the application to switch between different video players seamlessly.
    /// </summary>
    public interface VideoPlayerInterface : IDisposable
    {
        Task OpenAsync(
            PlaybackResource resource,
            CancellationToken cancellationToken);

        void Play();

        void Pause();

        void Stop();

        void Seek(
            double position);

        double Position { get; }

        double Duration { get; }

        bool IsPlaying { get; }

        object NativePlayer { get; }
    }
}
