using Dalamud.Configuration;
using System;
using System.IO;
using System.Runtime.Serialization;   // ← für OnDeserializedAttribute

namespace Soundy
{
    [Serializable]
    public sealed class Configuration : IPluginConfiguration
    {
        private const int CurrentConfigVersion = 2;   // ↔ Plugin-Version 1.1
        public int Version { get; set; } = CurrentConfigVersion;

        public string PenumbraPath { get; set; } = "";
        public float Volume { get; set; } = 1.0f;
        public bool AreToolsDownloaded { get; set; }

        // ▸ Default = true, weil Neuinstallationen nichts aufräumen müssen
        public bool TempCleanupDone { get; set; } = true;

        public int Choice { get; set; }
        public string ToolsZipUrl { get; set; } =
            "https://github.com/lnjanos/yueImport/releases/download/release/tools.zip";

        public static string BasePath => Plugin.PluginInterface.GetPluginConfigDirectory();
        public static string ToolsPath => Path.Combine(BasePath, "tools");
        public static string Resources => Path.Combine(BasePath, "resources");

        /* ----------------  MIGRATION CALLBACK  ---------------- */
        [OnDeserialized]                     // wird NACH dem Laden aufgerufen
        private void Migrate(StreamingContext _)
        {
            if (Version < CurrentConfigVersion)   // ⇒ Upgrade von 1.0
            {
                TempCleanupDone = false;          // Cleanup einmalig nötig
                Version = CurrentConfigVersion;
                Save();                           // gleich wegschreiben
            }
            // Neuinstallationen kommen hier gar nicht rein (Version==2),
            // TempCleanupDone bleibt daher auf true.
        }
        /* ------------------------------------------------------ */

        public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
    }
}
