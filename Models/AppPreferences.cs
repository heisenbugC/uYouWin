namespace uYouWin.Models
{
    /// <summary>
    /// General user preferences not tied to a specific service, persisted
    /// alongside the other Data-folder settings files.
    /// </summary>
    public class AppPreferences
    {
        public string RecommendationRegion { get; set; }


        public AppPreferences()
        {
            RecommendationRegion = string.Empty;
        }
    }
}
