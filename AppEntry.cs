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

        /// <summary>Native high-res Windows icon extracted at runtime.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? AppIcon { get; set; }
    }

    public class CategoryModel
    {
        public string CategoryName { get; set; } = "General";
        public string IconCode { get; set; } = "\uE71D"; // Default App Icon
        public System.Collections.Generic.List<AppEntry> Apps { get; set; } = new();
    }

    public class AppDataRoot
    {
        public System.Collections.Generic.List<CategoryModel> Categories { get; set; } = new();
        public System.Collections.Generic.List<string> WatchedFolders { get; set; } = new();
        public System.Collections.Generic.List<ProjectModel> ManualProjects { get; set; } = new();
    }

    public class ProjectModel
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public System.DateTime LastModified { get; set; }
        public string Extension { get; set; } = "";
        public string AppId { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? DisplayIcon { get; set; }
    }
}
