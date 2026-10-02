using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uYouWin.Models;

namespace uYouWin.Services.Archive
{
    internal class GoogleTakeoutImporter : ArchiveImporterInterface
    {
        public Task<List<Subscription>> ImportSubscriptionsAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ParseSubscriptions(path));
        }

        public Task<List<HistoryEntry>> ImportHistoryAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ParseWatchHistory(path));
        }

        public Task<List<Playlist>> ImportPlaylistsAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ParsePlaylists(path));
        }

        public Task<List<SearchHistoryEntry>> ImportSearchHistoryAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ParseSearchHistory(path));
        }

        public async Task<ArchiveImportResult> ImportAsync(
            string path,
            CancellationToken cancellationToken)
        {
            var result = new ArchiveImportResult();

            if (string.IsNullOrWhiteSpace(path))
                return result;

            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.csv", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Merge(result, await ImportFileAsync(file, cancellationToken));
                }

                return result;
            }

            return await ImportFileAsync(path, cancellationToken);
        }

        private async Task<ArchiveImportResult> ImportFileAsync(
            string path,
            CancellationToken cancellationToken)
        {
            var result = new ArchiveImportResult();
            string name = Path.GetFileName(path) ?? string.Empty;

            if (name.IndexOf("subscription", StringComparison.OrdinalIgnoreCase) >= 0)
                result.Subscriptions = await ImportSubscriptionsAsync(path, cancellationToken);
            else if (name.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0)
                result.SearchHistory = await ImportSearchHistoryAsync(path, cancellationToken);
            else if (name.IndexOf("watch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("history", StringComparison.OrdinalIgnoreCase) >= 0)
                result.History = await ImportHistoryAsync(path, cancellationToken);
            else
                result.Playlists = await ImportPlaylistsAsync(path, cancellationToken);

            return result;
        }

        private static void Merge(ArchiveImportResult target, ArchiveImportResult source)
        {
            target.Subscriptions.AddRange(source.Subscriptions);
            target.Playlists.AddRange(source.Playlists);
            target.History.AddRange(source.History);
            target.SearchHistory.AddRange(source.SearchHistory);
        }

        private static List<Subscription> ParseSubscriptions(string path)
        {
            var rows = ReadCsv(path);
            var results = new List<Subscription>();

            if (rows.Count == 0)
                return results;

            int start = 0;
            int idColumn = 0;
            int urlColumn = 1;
            int titleColumn = 2;

            if (IsHeader(rows[0]))
            {
                idColumn = FindColumn(rows[0], "channel id", "channelid", "id");
                urlColumn = FindColumn(rows[0], "channel url", "url");
                titleColumn = FindColumn(rows[0], "channel title", "channel name", "title", "name");
                start = 1;
            }

            for (int i = start; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string id = Cell(row, idColumn);
                string url = Cell(row, urlColumn);
                string title = Cell(row, titleColumn);

                if (string.IsNullOrWhiteSpace(id))
                    id = ExtractChannelId(url);

                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(title))
                    continue;

                results.Add(new Subscription
                {
                    ChannelId = id,
                    ChannelUrl = string.IsNullOrWhiteSpace(url)
                        ? "https://www.youtube.com/channel/" + id
                        : url,
                    ChannelName = string.IsNullOrWhiteSpace(title) ? id : title
                });
            }

            return results;
        }

        private static List<HistoryEntry> ParseWatchHistory(string path)
        {
            var rows = ReadCsv(path);
            var results = new List<HistoryEntry>();

            if (rows.Count == 0)
                return results;

            int start = 0;
            int idColumn = 0;
            int titleColumn = 1;
            int urlColumn = -1;
            int timeColumn = -1;
            int channelColumn = -1;

            if (IsHeader(rows[0]))
            {
                idColumn = FindColumn(rows[0], "video id", "videoid", "id");
                titleColumn = FindColumn(rows[0], "title", "video title");
                urlColumn = FindColumn(rows[0], "url", "video url", "title url", "titleUrl", "link");
                timeColumn = FindColumn(rows[0], "time", "watched", "watched at", "date", "timestamp");
                channelColumn = FindColumn(rows[0], "channel", "channel title");
                start = 1;
            }

            for (int i = start; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string id = Cell(row, idColumn);
                string url = Cell(row, urlColumn);
                string title = Cell(row, titleColumn);

                id = ExtractVideoId(id);
                if (string.IsNullOrWhiteSpace(id))
                    id = ExtractVideoId(url);

                if (string.IsNullOrWhiteSpace(id))
                    id = ExtractVideoId(title);

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(id))
                    url = "https://www.youtube.com/watch?v=" + id;

                results.Add(new HistoryEntry
                {
                    VideoId = id,
                    Title = string.IsNullOrWhiteSpace(title) ? id : title,
                    ChannelTitle = Cell(row, channelColumn),
                    WebUrl = url,
                    WatchedAt = ParseTime(Cell(row, timeColumn))
                });
            }

            return results;
        }

        private static List<Playlist> ParsePlaylists(string path)
        {
            var rows = ReadCsv(path);
            var results = new List<Playlist>();

            if (rows.Count == 0)
                return results;

            int videoColumn = 0;
            int titleColumn = -1;
            int playlistColumn = -1;
            int start = 0;
            string fileTitle = Path.GetFileNameWithoutExtension(path);

            for (int i = 0; i < rows.Count; i++)
            {
                int idIndex = FindColumn(rows[i], "video id", "videoid", "video url");
                if (idIndex >= 0)
                {
                    if (i > 0)
                        rows = rows.GetRange(i, rows.Count - i);
                    break;
                }
                int nameIndex = FindColumn(rows[i], "title");
                if (nameIndex >= 0 && i + 1 < rows.Count)
                    fileTitle = Cell(rows[i + 1], nameIndex);
            }

            if (IsHeader(rows[0]))
            {
                videoColumn = FindColumn(rows[0], "video id", "videoid", "video url", "id");
                titleColumn = FindColumn(rows[0], "video title", "title");
                playlistColumn = FindColumn(rows[0], "playlist title", "playlist");
                start = 1;
            }

            var byTitle = new Dictionary<string, Playlist>(StringComparer.OrdinalIgnoreCase);

            for (int i = start; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string videoId = ExtractVideoId(Cell(row, videoColumn));
                string videoTitle = Cell(row, titleColumn);
                string playlistTitle = Cell(row, playlistColumn);

                if (string.IsNullOrWhiteSpace(videoId))
                    videoId = ExtractVideoId(videoTitle);

                if (string.IsNullOrWhiteSpace(videoId))
                    continue;

                if (string.IsNullOrWhiteSpace(playlistTitle))
                    playlistTitle = fileTitle;

                Playlist playlist;

                if (!byTitle.TryGetValue(playlistTitle, out playlist))
                {
                    playlist = new Playlist
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = playlistTitle
                    };
                    byTitle[playlistTitle] = playlist;
                    results.Add(playlist);
                }

                if (playlist.VideoIds.Contains(videoId))
                    continue;

                playlist.VideoIds.Add(videoId);
                playlist.Videos.Add(new Video
                {
                    Id = videoId,
                    Title = string.IsNullOrWhiteSpace(videoTitle) ? videoId : videoTitle,
                    YtUrl = "https://www.youtube.com/watch?v=" + videoId,
                    WebUrl = "https://www.youtube.com/watch?v=" + videoId
                });
            }

            return results;
        }

        private static List<SearchHistoryEntry> ParseSearchHistory(string path)
        {
            var rows = ReadCsv(path);
            var results = new List<SearchHistoryEntry>();

            if (rows.Count == 0)
                return results;

            int start = 0;
            int queryColumn = 0;
            int timeColumn = -1;

            if (IsHeader(rows[0]))
            {
                queryColumn = FindColumn(rows[0], "query", "search", "title", "text");
                timeColumn = FindColumn(rows[0], "time", "date", "timestamp");
                start = 1;
            }

            for (int i = start; i < rows.Count; i++)
            {
                string query = Cell(rows[i], queryColumn);

                if (string.IsNullOrWhiteSpace(query))
                    continue;

                results.Add(new SearchHistoryEntry
                {
                    Query = query,
                    SearchedAt = ParseTime(Cell(rows[i], timeColumn))
                });
            }

            return results;
        }

        private static List<string[]> ReadCsv(string path)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("CSV file not found.", path);
            var record = new StringBuilder();
            bool quoted = false;
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                int value;
                while ((value = reader.Read()) >= 0)
                {
                    char c = (char)value;
                    if (c == '"')
                    {
                        record.Append(c);
                        if (quoted && reader.Peek() == '"')
                            record.Append((char)reader.Read());
                        else
                            quoted = !quoted;
                    }
                    else if ((c == '\r' || c == '\n') && !quoted)
                    {
                        if (record.Length > 0)
                            rows.Add(SplitCsvLine(record.ToString()));
                        record.Clear();
                        if (c == '\r' && reader.Peek() == '\n')
                            reader.Read();
                    }
                    else
                        record.Append(c);
                }
            }
            if (quoted)
                throw new InvalidDataException("CSV contains an unterminated quoted field.");
            if (record.Length > 0)
                rows.Add(SplitCsvLine(record.ToString()));
            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }

                    continue;
                }

                if (c == ',' && !quoted)
                {
                    cells.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            cells.Add(current.ToString().Trim());
            return cells.ToArray();
        }

        private static bool IsHeader(string[] row)
        {
            if (row == null)
                return false;

            return FindColumn(row, "channel id", "video id", "title", "query", "search", "playlist title", "url", "time") >= 0;
        }

        private static int FindColumn(string[] header, params string[] names)
        {
            for (int i = 0; i < header.Length; i++)
            {
                string cell = (header[i] ?? string.Empty).Trim().TrimStart('\uFEFF').ToLowerInvariant();

                foreach (string name in names)
                {
                    if (cell == name || cell.Replace(" ", string.Empty) == name.Replace(" ", string.Empty))
                        return i;
                }
            }

            return -1;
        }

        private static string Cell(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length)
                return string.Empty;

            return row[index] ?? string.Empty;
        }

        private static string ExtractVideoId(string value)
        {
            return Services.Cache.VisitedUrlCache.VideoIdFromUrl(value) ?? string.Empty;
        }

        private static string ExtractChannelId(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            const string marker = "/channel/";
            int index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
                return string.Empty;

            string id = url.Substring(index + marker.Length);
            int slash = id.IndexOf('/');

            if (slash >= 0)
                id = id.Substring(0, slash);

            return id.Trim();
        }

        private static DateTime ParseTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.Now;

            DateTime parsed;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
                return parsed;

            if (DateTime.TryParse(value, out parsed))
                return parsed;

            return DateTime.Now;
        }
    }
}
