using Dalamud.Configuration;
using System;
using System.IO;

namespace Soundy
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // User-defined paths
        public string PenumbraPath { get; set; } = "";

        public float Volume = 1.0f;

        // Tool download status
        public bool AreToolsDownloaded { get; set; } = false;

        public int Choice { get; set; } = 0;

        // Download URL for the tools ZIP
        public string ToolsZipUrl { get; set; } = "https://github.com/lnjanos/yueImport/releases/download/release/tools.zip";

        // Base directories
        public static string BasePath { get; } = Plugin.PluginInterface.GetPluginConfigDirectory();
        public static string ToolsPath { get; } = Path.Combine(BasePath, "tools");
        public static string Resources { get; } = Path.Combine(BasePath, "resources");
        
        public void SetVolume(float vol)
        {
            this.Volume = vol;
        }

        public float GetVolume()
        {
            return this.Volume;
        }

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
