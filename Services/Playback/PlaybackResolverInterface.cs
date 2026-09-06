using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Playback
{
    internal interface PlaybackResolverInterface
    {
        Task<PlaybackResource> ResolveAsync(
            Video video,
            PlaybackSettings settings,
            CancellationToken cancellationToken);
    }
}
