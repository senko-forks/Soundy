using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web; // Für URL-Parsing, benötigt .NET Framework oder entsprechendes Paket

namespace SamplePlugin
{
    public static class YoutubeDownloader
    {
        /// <summary>
        /// Lädt via yt-dlp nur den Audiostream von YouTube,
        /// konvertiert ihn sofort in MP3 oder Vorbis (OGG).
        /// Ignoriert Playlists und lädt immer nur das einzelne Video.
        /// </summary>
        /// <param name="youtubeUrl">Die YouTube-URL (z. B. https://youtube.com/watch?v=123)</param>
        /// <param name="outputFile">Zielpfad, z. B. "C:\XY\mysong.mp3" oder ".ogg"</param>
        /// <param name="useMp3">true = MP3, false = Vorbis/OGG</param>
        public static async Task DownloadAudioAsync(string youtubeUrl, string outputFile, bool useMp3 = true)
        {
            // Pfad zu yt-dlp
            var exePath = ToolLoader.YtdlpPath;

            // Argumente zusammenstellen
            // --extract-audio -> extrahiert nur den Audio-Stream
            // --audio-format -> mp3 oder vorbis
            // --audio-quality 0 -> beste Qualität
            // --no-playlist -> ignoriert Playlists und lädt nur das einzelne Video
            // -o "<Pfad>"
            string format = useMp3 ? "mp3" : "vorbis";

            string args = $"--extract-audio --audio-format {format} --audio-quality 0 --no-playlist -o \"{outputFile}\" \"{youtubeUrl}\"";

            Plugin.Log.Information($"[yt-dlp] Starte Prozess mit Argumenten: {args}");
            await RunProcessAsync(exePath, args);
        }

        private static async Task RunProcessAsync(string exePath, string arguments)
        {
            var si = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = si, EnableRaisingEvents = true };

            // Event-Handler für Ausgaben
            proc.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Plugin.Log.Information($"[yt-dlp OUTPUT] {e.Data}");
                }
            };

            proc.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Plugin.Log.Warning($"[yt-dlp ERROR] {e.Data}");
                }
            };

            try
            {
                proc.Start();
                Plugin.Log.Information($"[yt-dlp] Prozess gestartet mit PID {proc.Id}");

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                // Optional: Timeout setzen, z.B. 5 Minuten
                var timeout = TimeSpan.FromMinutes(5);
                bool exited = await Task.Run(() => proc.WaitForExit((int)timeout.TotalMilliseconds));

                if (!exited)
                {
                    proc.Kill();
                    Plugin.Log.Error("[yt-dlp] Prozess aufgrund von Timeout beendet.");
                    throw new TimeoutException("yt-dlp Prozess ist aufgrund eines Timeouts abgestürzt.");
                }

                Plugin.Log.Information($"[yt-dlp] Prozess beendet mit Exit-Code {proc.ExitCode}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[yt-dlp] Ausnahme: {ex.Message}");
                throw;
            }
        }
    }
}
