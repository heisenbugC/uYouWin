using System;
using System.IO;
using Newtonsoft.Json;
using uYouWin.Models;
using uYouWin.Services.Storage;

namespace uYouWin.Services.Preferences
{
    /// <summary>
    /// Loads and persists <see cref="AppPreferences"/> to the Data folder.
    /// </summary>
    internal static class AppPreferencesStore
    {
        private static string FilePath
        {
            get
            {
                return DataPathProvider.GetFilePath(
                    "app-preferences.json");
            }
        }

        public static AppPreferences Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new AppPreferences();

                string json = File.ReadAllText(FilePath);

                AppPreferences preferences =
                    JsonConvert.DeserializeObject<AppPreferences>(json);

                return preferences ?? new AppPreferences();
            }
            catch
            {
                return new AppPreferences();
            }
        }

        public static void Save(AppPreferences preferences)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));

            string filePath = FilePath;

            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) &&
                !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(
                preferences,
                Formatting.Indented);

            File.WriteAllText(filePath, json);
        }
    }
}
