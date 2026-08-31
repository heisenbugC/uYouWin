using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Invidious
{
    internal interface InvidiousServiceInterface
    {
        Task<Video> GetVideoAsync(string videoId);

        Task<Channel> GetChannelAsync(string channelId);

        Task<List<Video>> SearchAsync(string query);

        Task<List<Video>> GetChannelVideoAsync(string channelId);

        Task<List<Video>> GetPlaylistVideoAsync(string playlistId);
    }
}
