using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Soundy.Windows;

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
                await YoutubeDownloader.DownloadAudioAsync(youtubeUrl, tempWav, useMp3: true);
                // useMp3:true => wir haben "wav" eingetragen -> yt-dlp generiert "wav"

                if (!File.Exists(tempWav))
                {
                    throw new FileNotFoundException("yt-dlp did not produce the expected WAV file.", tempWav);
                }

                // 3) ffmpeg-Aufruf -> OGG (libvorbis), 44100 Hz, Volume, optional Limiter
                main.ChangeState("Converting MP3 to OGG...", true);
                Plugin.Log.Information($"Converting MP3 to OGG:");
                ConvertWavToOggHighQuality(tempWav, outputOggFile, userVolume, applyLimiter, quality);
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
            // clamp
            quality = Math.Clamp(quality, 0, 10);

            // z. B. lineare Skala userVolume=1..5 -> factor=1.0..2.0 (oder so)
            //float volumeFactor = UserValueToVolumeFactor(userVolume);
            float volumeFactor = 2.0f;

            // Limiter hinzufügen?
            //   Falls du die Bässe nicht clippen willst, macht "alimiter" Sinn.
            //   So z. B.: "volume=1.8,alimiter=limit=0.98"
            var filterParts = $"volume={volumeFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            if (useLimiter && volumeFactor > 1.0f)
            {
                filterParts += ",alimiter=limit=0.99";
            }

            // Falls userVolume ~1 => kein Filter
            // Ggf. ab userVolume>1.01 => Filter
            string filterArg = (Math.Abs(volumeFactor - 1.0f) > 0.01f || useLimiter)
                ? $"-filter:a \"{filterParts}\""
                : ""; // kein Filter

            // ffmpeg: -y -> überschreibt Ziel ohne Rückfrage
            //         -ac 2 -> stereo
            //         -ar 44100 -> sample rate
            //         -c:a libvorbis -> Vorbis
            //         -q:a <x>  -> 0..10 (10 = max. Qualität)
            string args = $"-y -i \"{wavInput}\" " +
                          "-vn -map_metadata -1 " +
                          "-ac 2 -ar 44100 " +
                          $"{filterArg} " +
                          $"-c:a libvorbis -q:a {quality} " +
                          $"\"{oggOutput}\"";

            RunFfmpeg(args);
        }


        /// <summary>Ganz einfache lineare Funktion: [1..5] -> [1..2] o.Ä. 
        /// Entweder selbst anpassen, z. B. max=1.8 usw.</summary>
        private static float UserValueToVolumeFactor(float userValue)
        {
            userValue = Math.Clamp(userValue, 1.0f, 10.0f);
            // Beispiel: 1 => 1.0 , 5 => 2.0
            // (2.0 - 1.0)/(5-1)=0.25 => 1 + (userValue-1)*0.25 => 1..2
            // Du kannst es auch so lösen:
            float minVol = 1.0f;
            float maxVol = 10.0f; // hier 2.0 = +6dB
            return minVol + (userValue - 1f) * ((maxVol - minVol) / 4f);
        }


        /// <summary>
        /// Startet einen ffmpeg-Prozess synchron (ggf. async), wirf Exception bei Fehler.
        /// </summary>
        private static void RunFfmpeg(string arguments, int timeoutMs = 60_000)
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
