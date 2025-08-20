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
        public bool ffmpegMissing { get; set; } = false;
        public bool ytdlpMissing { get; set; } = false;

        public int Choice { get; set; }
        public readonly string ToolsZipUrl =
            "https://github.com/lnjanos/yueImport/releases/download/release/tools.zip";

        public readonly string ffmpegZipUrl =
            "https://github.com/lnjanos/yueImport/releases/download/release/ffmpeg.zip";

        public readonly string ytdlpUrl =
            "https://github.com/lnjanos/yueImport/releases/download/release/yt-dlp.zip"; 

        public static string BasePath => Plugin.PluginInterface.GetPluginConfigDirectory();
        public static string ToolsPath => Path.Combine(BasePath, "tools");
        public static string Resources => Path.Combine(BasePath, "resources");

        public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
    }
}
