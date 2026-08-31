using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Library
{
    internal interface LibraryServiceInterface
    {
        Task<List<Subscription>> GetSubscriptionsAsync();

        Task<List<Playlist>> GetPlaylistsAsync();

        Task<List<HistoryEntry>> GetHistoryAsync();

        Task AddSubscriptionAsync(Subscription subscription);

        Task AddPlaylistAsync(Playlist playlist);

        Task AddHistoryEntryAsync(HistoryEntry entry);
    }
}
