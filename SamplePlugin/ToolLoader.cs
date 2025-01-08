using System;
using System.IO;

namespace SamplePlugin
{
    public static class ToolLoader
    {
        private static bool extracted = false;

        public static string YtdlpPath { get; private set; } = "";
        public static string FfmpegPath { get; private set; } = "";

        /// <summary>
        /// Liest die .exe-Pfade aus Configuration.Tools und speichert sie in YtdlpPath und FfmpegPath.
        /// Wir gehen davon aus, dass Configuration.Tools ein Verzeichnis ist, in dem ffmpeg.exe und yt-dlp.exe liegen.
        /// </summary>
        public static void ExtractTools()
        {
            if (extracted)
                return;

            // 1) Hol dir den Pfad aus deiner Plugin-Config, z. B.
            //    Plugin.Instance.Configuration.Tools = "C:\some\tools"
            var toolsDir = Configuration.Tools;
            if (string.IsNullOrEmpty(toolsDir))
            {
                throw new Exception("Configuration.Tools is not set. Cannot locate ffmpeg.exe and yt-dlp.exe.");
            }

            if (!Directory.Exists(toolsDir))
            {
                throw new DirectoryNotFoundException($"Tools directory not found: {toolsDir}");
            }

            // 2) Baue die vollständigen Pfade
            var ffmpegExe = Path.Combine(toolsDir, "ffmpeg.exe");
            var ytdlpExe = Path.Combine(toolsDir, "yt-dlp.exe");

            // 3) Prüfe, ob die Dateien existieren
            if (!File.Exists(ffmpegExe))
            {
                throw new FileNotFoundException($"ffmpeg.exe not found in {toolsDir}");
            }

            if (!File.Exists(ytdlpExe))
            {
                throw new FileNotFoundException($"yt-dlp.exe not found in {toolsDir}");
            }

            // 4) Setze sie als Pfade
            FfmpegPath = ffmpegExe;
            YtdlpPath = ytdlpExe;

            extracted = true;
        }
    }
}
