using Lumina.Excel.Sheets;
using System;
using System.IO;
using System.Reflection;

namespace YTImport
{
    public static class ResourceHelper
    {
        /// <summary>
        /// Extrahiert eine eingebettete Ressource (z. B. ffmpeg.exe, yt-dlp.exe)
        /// in den Temp-Ordner (oder einen gewünschten Pfad) und gibt diesen Pfad zurück.
        /// </summary>
        /// <param name="resourceName">
        /// Vollständiger Name, z. B. "YTImport.Resources.ffmpeg.exe"
        /// (siehe Assembly.GetManifestResourceNames()).
        /// </param>
        /// <param name="outputFileName">Wie die Datei heißen soll (z. B. "ffmpeg.exe").</param>
        /// <returns>Pfad zur extrahierten Datei (z. B. C:\Users\<user>\AppData\Local\Temp\ffmpeg.exe)</returns>
        public static string ExtractResourceToTemp(string resourceName, string outputFileName)
        {
            // Zielpfad (Temp)
            string tempPath = Path.Combine(Path.GetTempPath(), outputFileName);

            // Vorherige ggf. löschen
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            // Ressource aus aktueller Assembly öffnen
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new Exception($"Resource {resourceName} not found.");

            // In Datei kopieren
            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);

            return tempPath;
        }
    }
}
