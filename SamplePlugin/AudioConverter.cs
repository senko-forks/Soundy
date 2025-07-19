using System;
using System.Diagnostics;
using VfxEditor.ScdFormat;

namespace Soundy
{
    public static class AudioConverter
    {
        // Statische Variable, um den derzeitigen ffmpeg-Prozess zu tracken
        private static Process? currentFfmpegProcess = null;
        private static readonly object ffmpegLock = new object();



        /// <summary>
        /// Konvertiert eine Audiodatei (z. B. MP3) in OGG (44100 Hz) via ffmpeg.
        /// </summary>
        public static void ConvertToOgg44100(string inputFile, string outputFile, float volume = 1.0f)
        {
            var exePath = ToolLoader.FfmpegPath;

            var vol = UserValueToVolumeFactor(volume);

            // Falls volume nahe 1.0, kann man den Filter weglassen, um unnötiges Rekodieren zu vermeiden
            // oder man lässt ihn immer drin, wie man mag.
            // string volumeFilter = $"-filter:a \"volume={vol}\""; <-- alt
            string volumeFilter = "";
            if (Math.Abs(vol - 1.0f) > 0.01f)
            {
                volumeFilter = $"-filter:a \"volume={vol.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"";
            }

            string args = $"-y -i \"{inputFile}\" " +
                          "-vn -map_metadata -1 " +
                          "-ac 2 -ar 44100 " +
                          $"{volumeFilter} " +
                          "-c:a libvorbis -q:a 8 " +
                          $"\"{outputFile}\"";


            RunProcess(exePath, args, 30000);
        }

        public static float UserValueToVolumeFactor(float userValue)
        {
            // clamp userValue an, falls er versehentlich <1 oder >5 sein könnte
            userValue = Math.Clamp(userValue, 1.0f, 5.0f);

            // Dann per lineare Funktion: f(x) = 0.625*x + 0.375
            return 0.625f * userValue + 0.375f;
        }



        /// <summary>
        /// Startet einen FFmpeg-Prozess mit Argumenten und setzt einen Timeout. 
        /// Falls sich FFmpeg aufhängt oder länger als <paramref name="timeoutMs"/> benötigt,
        /// wird der Prozess gekillt. Außerdem wird ein ggf. noch laufender FFmpeg-Prozess 
        /// zuvor beendet.
        /// </summary>
        private static void RunProcess(string exePath, string arguments, int timeoutMs)
        {
            lock (ffmpegLock)
            {
                // Prüfe, ob noch ein älterer FFmpeg-Prozess läuft
                if (currentFfmpegProcess != null && !currentFfmpegProcess.HasExited)
                {
                    Plugin.Log.Warning("FFmpeg was still running – killing old process...");
                    try
                    {
                        currentFfmpegProcess.Kill();
                    }
                    catch (Exception killEx)
                    {
                        Plugin.Log.Error($"Error killing previous ffmpeg: {killEx.Message}");
                    }
                    finally
                    {
                        currentFfmpegProcess.Dispose();
                        currentFfmpegProcess = null;
                    }
                }

                // Neuen Prozess vorbereiten
                var si = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = si };

                if (!proc.Start())
                    throw new Exception($"Failed to start process {exePath}");

                currentFfmpegProcess = proc;

                // Asynchron Ausgaben lesen
                var stdOutTask = proc.StandardOutput.ReadToEndAsync();
                var stdErrTask = proc.StandardError.ReadToEndAsync();

                // Auf Beendigung mit Timeout warten
                bool exited = proc.WaitForExit(timeoutMs);
                if (!exited)
                {
                    // Timeout -> kill
                    Plugin.Log.Warning($"FFmpeg timed out after {timeoutMs}ms. Killing process...");
                    try
                    {
                        proc.Kill();
                    }
                    catch (Exception killEx)
                    {
                        Plugin.Log.Error($"Error killing ffmpeg: {killEx.Message}");
                    }
                    throw new Exception("FFmpeg timed out");
                }

                // Jetzt Ausgaben einsammeln
                string output = stdOutTask.Result;
                string error = stdErrTask.Result;

                // Code auswerten
                if (proc.ExitCode != 0)
                {
                    Plugin.Log.Warning($"[ffmpeg error code: {proc.ExitCode}]\n{error}");
                }
                else
                {
                    Plugin.Log.Information($"[ffmpeg output]\n{output}");
                    if (!string.IsNullOrEmpty(error))
                        Plugin.Log.Warning($"[ffmpeg error]\n{error}");
                }

                // Prozess weg
                proc.Dispose();
                currentFfmpegProcess = null;
            }
        }
    }
}
