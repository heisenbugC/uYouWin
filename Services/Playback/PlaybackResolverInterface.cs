using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    public interface PlaybackResolverInterface
    {
        Task<PlaybackResource> ResolveAsync(
            Video video,
            PlaybackSettings settings,
            CancellationToken cancellationToken);
    }
}
