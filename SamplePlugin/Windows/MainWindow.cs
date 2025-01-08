using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks; // Wichtig für async/await
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using BuildScd;
using ImGuiNET;
using Lumina.Excel.Sheets;

namespace SamplePlugin.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private Plugin plugin;
        private string goatImagePath;

        // Eingabefeld
        private string saveName = "";
        private string youtubeLink = "";
        private string playlist = "";

        private string proccessState = "";

        // Damit wir eine laufende Aufgabe tracken können
        private Task? currentDownloadTask;

        public MainWindow(Plugin plugin)
            : base(
                "YT Import",
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(375, 150),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };

            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (plugin.Configuration.DJPath == "")
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Save your DJ Path in the settings.");
                if (ImGui.Button("Show Settings"))
                {
                    plugin.ToggleConfigUI();
                }
                return;
            }

            ImGui.Spacing();

            // Eingabefeld für den YouTube-Link
            ImGui.InputText("YouTube Link##myyt", ref youtubeLink, 1024);
            ImGui.InputText("Saving Name##name", ref saveName, 1024);
            ImGui.InputText("Playlist##name", ref playlist, 1024);

            // Button, der das asynchrone Herunterladen/Konvertieren startet
            if (ImGui.Button("Download & Save to DJ"))
            {
                // Wenn bereits eine Aufgabe läuft, nicht doppelt starten
                if (currentDownloadTask == null || currentDownloadTask.IsCompleted)
                {
                    // Fire-and-forget: Wir starten die Methode und speichern uns das Task-Objekt.
                    // So blockiert die UI nicht.
                    currentDownloadTask = DownloadConvertAsync();
                }
            }

            // Aktueller Status
            if (!string.IsNullOrEmpty(proccessState))
            {
                ImGui.Text(proccessState);
            }
        }

        /// <summary>
        /// Asynchrone Methode, um MP3 herunterzuladen, zu OGG zu konvertieren
        /// und danach SCD zu erzeugen.
        /// </summary>
        private async Task DownloadConvertAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(youtubeLink))
                {
                    proccessState = "No YouTube link provided.";
                    Plugin.Log.Warning("No YouTube link provided.");
                    return;
                }

                proccessState = "Preparing download...";
                await Task.Yield(); // Kleiner "Trick", um sicherzustellen, dass wir die UI nicht blocken

                // 1) Zielpfad definieren
                //    Z.B. "C:\FFXIVSoundMods\mySong.ogg"

                ResourceChecker.CheckDJ(plugin);
                if (plugin.Configuration.DJPath == "")
                {
                    proccessState = "Error with Path. Renew the given Path in the Settings.";
                    return;
                }

                var rand = new Random().Next(9999);
                var rand2 = new Random().Next(9999);

                string finalOgg = Path.Combine(Path.GetTempPath(), $"{rand}_{saveName}_{rand2}.ogg");
                string tempMp3 = Path.Combine(Path.GetTempPath(), $"{rand}_{saveName}_{rand2}.mp3");
                string scdPath = Path.Combine(plugin.Configuration.DJPath, "custom", $"{rand}_{saveName}_{rand2}.scd");

                string samplePath = Path.Combine(Configuration.Resources, "test.scd");
                string sampleOggPath = Path.Combine(Configuration.Resources, "7957_kids bitte funnktionier_4072.ogg");

                // 2) Download in einem Threadpool-Thread (CPU-/IO-lastig)
                proccessState = "Downloading audio (yt-dlp)...";
                await Task.Run(async() =>
                {
                    Plugin.Log.Information($"Downloading {youtubeLink} -> {tempMp3}");
                    await YoutubeDownloader.DownloadAudioAsync(youtubeLink, tempMp3, useMp3: true);
                });

                // 3) Konvertieren (ffmpeg) -> OGG (44100 Hz)
                proccessState = "Converting MP3->OGG (ffmpeg)...";
                await Task.Run(() =>
                {
                    Plugin.Log.Information($"Converting MP3->OGG {tempMp3} -> {finalOgg}");
                    AudioConverter.ConvertToOgg44100(tempMp3, finalOgg);
                });

                // 4) ScdConverter (ebenfalls auslagern)
                proccessState = "Building SCD file...";
                await Task.Run(() =>
                {
                    Builder.BuildNewScd(samplePath, sampleOggPath, finalOgg, scdPath);
                });

                // Abschließende Meldung
                proccessState = $"Done.";
                DJImporter.Import(plugin, saveName, $"{rand}_{saveName}_{rand2}.scd", playlist);
                plugin.RefreshMods();
                
            }
            catch (Exception ex)
            {
                proccessState = $"Error: {ex.Message}";
                Plugin.Log.Error($"Error in DownloadConvertAsync: {ex.Message}");
            }
        }
    }
}
