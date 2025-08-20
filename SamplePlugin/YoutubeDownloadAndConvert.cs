using Soundy.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Soundy
{
    public static class YoutubeDownloadAndConvert
    {
        /// <summary>
        /// Lädt ein YouTube-Video via yt-dlp als WAV herunter, 
        /// und konvertiert es anschließend mit ffmpeg zu OGG (44100 Hz, Vorbis, hoher Qualität),
        /// wobei wir optional die Lautstärke (Volume) anheben und durch einen Limiter schützen.
        /// </summary>
        /// <param name="youtubeUrl">Die YouTube-URL.</param>
        /// <param name="outputOggFile">Ziel-OGG-Datei z. B. "C:\Temp\mySong.ogg"</param>
        /// <param name="userVolume">Z. B. 1..5, was intern per linearer Funktion zu 1.0..3.5 (oder weniger) mappt.</param>
        /// <param name="applyLimiter">true, um Clipping abzufangen (empfohlen, wenn userVolume > 1.0).</param>
        /// <param name="quality">Wert zwischen 0..10 für libvorbis. 10 = Maximum. 7..8 oft schon sehr gut.</param>
        public static async Task DownloadAndConvertAsync(string youtubeUrl, string outputOggFile, float userVolume, bool applyLimiter = true, int quality = 10, MainWindow main = null)
        {
            // 1) Temporäre WAV-Datei
            string tempWav = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wav");

            try
            {
                // 2) Via yt-dlp nur Audio extrahieren -> WAV
                //    So haben wir "verlustfrei" das Audio, das YouTube anbietet
                main.ChangeState("Downloading audio...");
                string tempAudio = await YoutubeDownloader.DownloadAudioAsync(youtubeUrl, tempWav, useMp3: true);
                // useMp3:true => wir haben "wav" eingetragen -> yt-dlp generiert "wav"

                if (!File.Exists(tempAudio))
                {
                    throw new FileNotFoundException("yt-dlp did not produce the expected WAV file.", tempAudio);
                }

                // 3) ffmpeg-Aufruf -> OGG (libvorbis), 44100 Hz, Volume, optional Limiter
                main.ChangeState("Converting MP3 to OGG...", true);
                Plugin.Log.Information($"Converting MP3 to OGG:");
                ConvertWavToOggHighQuality(tempAudio, outputOggFile, userVolume, applyLimiter, quality);
            }
            finally
            {
                // Temp-Datei bereinigen
                try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
            }
        }

        /// <summary>
        /// Ruft ffmpeg auf, um WAV -> OGG in hoher Qualität zu erzeugen.
        /// </summary>
        private static void ConvertWavToOggHighQuality(
            string wavInput,
            string oggOutput,
            float userVolume,
            bool useLimiter,
            int quality)
        {
            quality = 10;

            userVolume = 1.5f;

            var filters = new List<string>();

            // ① EBU R128 Loudness (–16 LUFS, 11 LRA, –1 dBTP)
            filters.Add("loudnorm=I=-16:LRA=11:TP=-1.0");

            // ② Zusätzlicher Gain (nur falls ≠1)
            if (Math.Abs(userVolume - 1f) > 0.01f)
                filters.Add($"volume={userVolume.ToString(CultureInfo.InvariantCulture)}");

            // ③ Limiter
            if (useLimiter)
                filters.Add("alimiter=limit=0.99");

            // ④ HQ-Resampling (SoXR) – nur aktiv, wenn Quell-SR ≠ 44 100 Hz
            filters.Add("aresample=resampler=soxr:precision=33");

            // ⑤ ffmpeg-Args
            string args =
                $"-y -i \"{wavInput}\" -vn -map_metadata -1 " +
                $"-af \"{string.Join(',', filters)}\" " +
                "-ar 44100 -ac 2 " +
                $"-c:a libvorbis -q:a {quality} -compression_level 10 " +
                $"\"{oggOutput}\"";

            RunFfmpeg(args);
        }

        /// <summary>
        /// Startet einen ffmpeg-Prozess synchron (ggf. async), wirf Exception bei Fehler.
        /// </summary>
        private static void RunFfmpeg(string arguments, int timeoutMs = 180_000)
        {
            var exePath = ToolLoader.FfmpegPath;

            var si = new ProcessStartInfo
            {
                FileName = exePath,
                // Banner & STDIN unterdrücken, sonst alles wie gehabt
                Arguments = "-nostdin -hide_banner -v info " + arguments,
                UseShellExecute = false,

                // wir wollen Log‑Zeilen => beide Pipes umleiten …
                RedirectStandardError = true,   // FFmpeg schreibt alles Wesentliche hierhin
                RedirectStandardOutput = true,   // Progress‑Zeilen landen gelegentlich auch hier
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = si, EnableRaisingEvents = true };

            // ---------- asynchron LEER LESEN = Puffer kann nicht mehr volllaufen ----------
            proc.ErrorDataReceived += (_, e) => {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Plugin.Log.Information($"[ffmpeg ERR] {e.Data}");
            };
            proc.OutputDataReceived += (_, e) => {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Plugin.Log.Information($"[ffmpeg OUT] {e.Data}");
            };

            try
            {
                proc.Start();
                Plugin.Log.Information($"[ffmpeg] gestartet (PID {proc.Id})");

                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                bool exited = proc.WaitForExit(timeoutMs);

                if (!exited)
                {
                    proc.Kill(entireProcessTree: true);
                    Plugin.Log.Error("[ffmpeg] Timeout – Prozess gekillt");
                    throw new TimeoutException("ffmpeg timed out");
                }

                Plugin.Log.Information($"[ffmpeg] beendet (Exit {proc.ExitCode})");

                if (proc.ExitCode != 0)
                    throw new Exception($"ffmpeg exit code {proc.ExitCode}");
            }
            catch
            {
                // Exception erneut werfen, damit dein Aufrufer denselben Flow behält
                throw;
            }
        }

    }
}
