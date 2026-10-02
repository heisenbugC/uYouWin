using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using uYouWin.Models;
using uYouWin.Services.Storage;

namespace uYouWin.Services.Library
{
    internal class LibraryService : LibraryServiceInterface
    {
        private readonly object _gate = new object();

        public Task<List<Subscription>> GetSubscriptionsAsync()
        {
            return Task.FromResult(LoadList<Subscription>("subscriptions.json"));
        }

        public Task<List<Playlist>> GetPlaylistsAsync()
        {
            return Task.FromResult(LoadList<Playlist>("playlists.json"));
        }

        public Task<List<HistoryEntry>> GetHistoryAsync()
        {
            List<HistoryEntry> history = LoadList<HistoryEntry>("watch-history.json");
            history.Sort((a, b) => b.WatchedAt.CompareTo(a.WatchedAt));
            return Task.FromResult(history);
        }

        public Task<List<SearchHistoryEntry>> GetSearchHistoryAsync()
        {
            List<SearchHistoryEntry> history =
                LoadList<SearchHistoryEntry>("search-history.json");

            history.Sort((a, b) => b.SearchedAt.CompareTo(a.SearchedAt));
            return Task.FromResult(history);
        }

        public Task AddSubscriptionAsync(Subscription subscription)
        {
            if (subscription == null || string.IsNullOrWhiteSpace(subscription.ChannelId))
                return Task.CompletedTask;

            lock (_gate)
            {
                List<Subscription> items = LoadList<Subscription>("subscriptions.json");

                if (!items.Any(s =>
                        string.Equals(s.ChannelId, subscription.ChannelId, StringComparison.Ordinal)))
                {
                    items.Add(subscription);
                    SaveList("subscriptions.json", items);
                }
            }

            return Task.CompletedTask;
        }

        public Task RemoveSubscriptionAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return Task.CompletedTask;

            lock (_gate)
            {
                List<Subscription> items = LoadList<Subscription>("subscriptions.json");
                items.RemoveAll(s =>
                    string.Equals(s.ChannelId, channelId, StringComparison.Ordinal));
                SaveList("subscriptions.json", items);
            }

            return Task.CompletedTask;
        }

        public Task AddPlaylistAsync(Playlist playlist)
        {
            if (playlist == null)
                return Task.CompletedTask;

            if (string.IsNullOrWhiteSpace(playlist.Id))
                playlist.Id = Guid.NewGuid().ToString("N");

            lock (_gate)
            {
                List<Playlist> items = LoadList<Playlist>("playlists.json");
                items.Add(playlist);
                SaveList("playlists.json", items);
            }

            return Task.CompletedTask;
        }

        public Task SavePlaylistAsync(Playlist playlist)
        {
            if (playlist == null || string.IsNullOrWhiteSpace(playlist.Id))
                return Task.CompletedTask;

            lock (_gate)
            {
                List<Playlist> items = LoadList<Playlist>("playlists.json");
                int index = items.FindIndex(p => p.Id == playlist.Id);

                if (index >= 0)
                    items[index] = playlist;
                else
                    items.Add(playlist);

                SaveList("playlists.json", items);
            }

            return Task.CompletedTask;
        }

        public Task DeletePlaylistAsync(string playlistId)
        {
            lock (_gate)
            {
                List<Playlist> items = LoadList<Playlist>("playlists.json");
                items.RemoveAll(p => p.Id == playlistId);
                SaveList("playlists.json", items);
            }

            return Task.CompletedTask;
        }

        public Task AddHistoryEntryAsync(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.VideoId))
                return Task.CompletedTask;

            lock (_gate)
            {
                List<HistoryEntry> items = LoadList<HistoryEntry>("watch-history.json");
                items.Insert(0, entry);


                SaveList("watch-history.json", items);
            }

            return Task.CompletedTask;
        }

        public Task AddSearchHistoryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Task.CompletedTask;

            lock (_gate)
            {
                List<SearchHistoryEntry> items =
                    LoadList<SearchHistoryEntry>("search-history.json");

                items.Insert(0, new SearchHistoryEntry
                {
                    Query = query.Trim(),
                    SearchedAt = DateTime.Now
                });


                SaveList("search-history.json", items);
            }

            return Task.CompletedTask;
        }

        public Task ImportSubscriptionsAsync(IEnumerable<Subscription> subscriptions)
        {
            if (subscriptions == null)
                return Task.CompletedTask;

            lock (_gate)
            {
                List<Subscription> items = LoadList<Subscription>("subscriptions.json");

                foreach (Subscription subscription in subscriptions)
                {
                    if (subscription == null || string.IsNullOrWhiteSpace(subscription.ChannelId))
                        continue;

                    if (items.Any(s =>
                            string.Equals(s.ChannelId, subscription.ChannelId, StringComparison.Ordinal)))
                        continue;

                    items.Add(subscription);
                }

                SaveList("subscriptions.json", items);
            }

            return Task.CompletedTask;
        }

        public Task ImportHistoryAsync(IEnumerable<HistoryEntry> history)
        {
            if (history == null)
                return Task.CompletedTask;

            lock (_gate)
            {
                List<HistoryEntry> items = LoadList<HistoryEntry>("watch-history.json");
                items.AddRange(history.Where(h => h != null && !string.IsNullOrWhiteSpace(h.VideoId)));
                items = items
                    .GroupBy(h => new { h.VideoId, h.WatchedAt }).Select(g => g.First())
                    .OrderByDescending(h => h.WatchedAt)
                    .ToList();
                SaveList("watch-history.json", items);
            }

            return Task.CompletedTask;
        }

        public Task ImportPlaylistsAsync(IEnumerable<Playlist> playlists)
        {
            if (playlists == null)
                return Task.CompletedTask;

            lock (_gate)
            {
                List<Playlist> items = LoadList<Playlist>("playlists.json");

                foreach (Playlist playlist in playlists)
                {
                    if (playlist == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(playlist.Id))
                        playlist.Id = Guid.NewGuid().ToString("N");

                    var existing = items.FirstOrDefault(p =>
                        p.Id == playlist.Id || string.Equals(p.Title, playlist.Title, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        foreach (Video video in playlist.Videos)
                        {
                            if (existing.VideoIds.Contains(video.Id)) continue;
                            existing.VideoIds.Add(video.Id);
                            existing.Videos.Add(video);
                        }
                        continue;
                    }

                    items.Add(playlist);
                }

                SaveList("playlists.json", items);
            }

            return Task.CompletedTask;
        }

        public Task ImportSearchHistoryAsync(IEnumerable<SearchHistoryEntry> entries)
        {
            if (entries == null)
                return Task.CompletedTask;

            lock (_gate)
            {
                List<SearchHistoryEntry> items =
                    LoadList<SearchHistoryEntry>("search-history.json");

                items.AddRange(entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Query)));
                items = items
                    .GroupBy(h => new { h.Query, h.SearchedAt }).Select(g => g.First())
                    .OrderByDescending(e => e.SearchedAt)
                    .ToList();
                SaveList("search-history.json", items);
            }

            return Task.CompletedTask;
        }

        private List<T> LoadList<T>(string fileName)
        {
            try
            {
                string path = DataPathProvider.GetFilePath(fileName);

                if (!File.Exists(path))
                    return new List<T>();

                List<T> items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path));
                return items ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Cannot read " + fileName + ". The existing file has not been overwritten.", ex);
            }
        }

        private void SaveList<T>(string fileName, List<T> items)
        {
            string path = DataPathProvider.GetFilePath(fileName);
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(items, Formatting.Indented));
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".bak");
            else
                File.Move(temporary, path);
        }
    }
}
