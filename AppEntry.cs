namespace WhiteLabelLauncher
{
    /// <summary>
    /// Represents a single application entry stored in apps.json.
    /// </summary>
    public class AppEntry
    {
        /// <summary>Unique identifier (GUID string) for stable referencing.</summary>
        public string Id { get; set; } = System.Guid.NewGuid().ToString();

        /// <summary>Display name shown under the icon.</summary>
        public string Name { get; set; } = "";

        /// <summary>Absolute path to the application's .exe file.</summary>
        public string ExePath { get; set; } = "";

        /// <summary>Optional short description / subtitle text.</summary>
        public string Description { get; set; } = "";

        /// <summary>Optional path to a custom icon image (overrides the gradient tile).</summary>
        public string IconPath { get; set; } = "";

        /// <summary>Optional 2-letter abbreviation shown inside the gradient tile (e.g. "Ai").</summary>
        public string Abbrev { get; set; } = "";
    }
}
