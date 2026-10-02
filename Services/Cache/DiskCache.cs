using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using uYouWin.Services.Storage;

namespace uYouWin.Services.Cache
{
    internal static class DiskCache
    {
        internal static readonly TimeSpan TimeToLive = TimeSpan.FromDays(30);
        private static readonly object Gate = new object();
        private static DateTime _nextCleanupUtc;

        internal static string Hash(string key)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
        }

        private static string GetPath(string category, string key)
        {
            string hash = Hash(category + "\n" + key);
            return Path.Combine(DataPathProvider.CacheDirectory, "v1", hash.Substring(0, 2), hash + ".json");
        }

        public static T TryGet<T>(string category, string key) where T : class
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (Gate)
            {
                try
                {
                    string path = GetPath(category, key);
                    if (!File.Exists(path)) return null;
                    using (var stream = File.OpenText(path))
                    using (var reader = new JsonTextReader(stream))
                    {
                        var entry = JsonSerializer.CreateDefault().Deserialize<Entry<T>>(reader);
                        if (entry != null && entry.SavedAtUtc <= DateTime.UtcNow &&
                            DateTime.UtcNow - entry.SavedAtUtc < TimeToLive)
                            return entry.Value;
                    }
                    File.Delete(path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (JsonException) { }
                return null;
            }
        }

        public static void Save<T>(string category, string key, T value) where T : class
        {
            if (string.IsNullOrEmpty(key) || value == null) return;
            lock (Gate)
            {
                string temporary = null;
                try
                {
                    string path = GetPath(category, key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    using (var stream = File.CreateText(temporary))
                    using (var writer = new JsonTextWriter(stream))
                        JsonSerializer.CreateDefault().Serialize(writer, new Entry<T>
                        {
                            SavedAtUtc = DateTime.UtcNow,
                            Value = value
                        });
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (JsonException) { }
                finally
                {
                    if (temporary != null)
                    {
                        try { File.Delete(temporary); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            ScheduleCleanup();
        }

        public static void ScheduleCleanup()
        {
            lock (Gate)
            {
                if (DateTime.UtcNow < _nextCleanupUtc) return;
                _nextCleanupUtc = DateTime.UtcNow.AddDays(1);
            }
            Task.Run((Action)RemoveExpired);
        }

        internal static void RemoveExpired()
        {
            try
            {
                string root = Path.Combine(DataPathProvider.CacheDirectory, "v1");
                if (!Directory.Exists(root)) return;
                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    lock (Gate)
                    {
                        try
                        {
                            TimeSpan age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
                            if ((path.EndsWith(".json", StringComparison.Ordinal) && age >= TimeToLive) ||
                                (path.EndsWith(".tmp", StringComparison.Ordinal) && age >= TimeSpan.FromDays(1)))
                                File.Delete(path);
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private sealed class Entry<T>
        {
            public DateTime SavedAtUtc { get; set; }
            public T Value { get; set; }
        }
    }
}
