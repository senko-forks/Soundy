using System;
using System.Diagnostics;

namespace SamplePlugin
{
    public static class AudioConverter
    {
        /// <summary>
        /// Konvertiert eine Audiodatei (z. B. MP3) in OGG (44100 Hz) via ffmpeg.
        /// </summary>
        public static void ConvertToOgg44100(string inputFile, string outputFile)
        {
            var exePath = ToolLoader.FfmpegPath;
            // Hier erzwingen wir:
            //  - Sample Rate: 44100  (-ar 44100)
            //  - Channels: 2         (-ac 2)
            //  - Volume: 1.0         (-af "volume=1.0")
            //  - Codec: libvorbis    (-c:a libvorbis)
            //
            // Anmerkung: "volume=1.0" ist standardmäßig Normalpegel, kann aber 
            // sinnvoll sein, wenn man Eingabedateien hat, die leiser sind, 
            // oder wenn man explizit die Lautstärke steuern will.

            string args = $"-i \"{inputFile}\" -ac 2 -ar 44100 -af \"volume=1.0\" -c:a libvorbis \"{outputFile}\"";
            RunProcess(exePath, args);
        }


        private static void RunProcess(string exePath, string arguments)
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

            using var proc = Process.Start(si);
            if (proc == null)
                throw new Exception($"Failed to start process {exePath}");

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            // Debug-Ausgaben
            Plugin.Log.Information($"[ffmpeg output]\n{output}");
            if (!string.IsNullOrEmpty(error))
                Plugin.Log.Warning($"[ffmpeg error]\n{error}");
        }
    }
}
