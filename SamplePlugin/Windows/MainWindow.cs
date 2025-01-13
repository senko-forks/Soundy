using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using BuildScd;
using ImGuiNET;
using ECommons.DalamudServices;

namespace YTImport.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        // Input fields
        private string saveName = "";
        private string youtubeLink = "";
        private string playlist = "";

        // Status messages
        private string processState = "";

        // Download status and progress
        private bool isDownloadingTools = false;
        private string downloadProgress = "";

        // Tracking ongoing tasks
        private Task? currentDownloadTask;

        public MainWindow(Plugin plugin)
            : base(
                  "YT Import",
                  ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 225),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            this.Size = new Vector2(400, 225);
            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            // If tools are not downloaded, show the download button
            if (!plugin.Configuration.AreToolsDownloaded)
            {
                if (isDownloadingTools)
                {
                    ImGui.Text($"Downloading tools... {downloadProgress}");
                    float progressValue = 0.0f;
                    if (double.TryParse(downloadProgress.Replace("%", ""), out double progress))
                    {
                        progressValue = (float)(progress / 100.0);
                    }
                    ImGui.ProgressBar(progressValue, new Vector2(200, 0), "Downloading tools...");
                }
                else
                {
                    if (ImGui.Button("Download Tools"))
                    {
                        StartDownloadTools();
                    }
                }
                return;
            }

            // Ensure tools are verified
            try
            {
                ToolLoader.VerifyTools(plugin);
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Tool verification failed: {ex.Message}");
                if (ImGui.Button("Re-download Tools"))
                {
                    StartDownloadTools();
                }
                return;
            }

            // Ensure DJPath is set
            if (plugin.Configuration.DJPath == "")
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Please set your DJ Path in the settings.");
                if (ImGui.Button("Show Settings"))
                {
                    plugin.ToggleConfigUI();
                }
                return;
            }

            ImGui.Spacing();

            // Input fields for YouTube link, save name, and playlist
            ImGui.InputText("YouTube Link##youtubeLink", ref youtubeLink, 1024);
            ImGui.InputText("Save Name##saveName", ref saveName, 1024);
            ImGui.InputText("Playlist##playlist", ref playlist, 1024);

            var choice = plugin.Configuration.Choice;
            string[] choices = ["BGM", "Sound Effects", "System Sounds"];

            if (ImGui.Combo("", ref choice, choices, 3))
            {
                plugin.Configuration.Choice = choice;
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            // Button to start download and conversion
            if (ImGui.Button("Download & Save to DJ"))
            {
                // Prevent multiple concurrent tasks
                if (currentDownloadTask == null || currentDownloadTask.IsCompleted)
                {
                    currentDownloadTask = DownloadConvertAsync();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Button to open DeleteWindow
            if (ImGui.Button("Delete Entries"))
            {
                plugin.ToggleDeleteUI(); // Toggle the DeleteWindow
            }

            // Display current status
            if (!string.IsNullOrEmpty(processState))
            {
                ImGui.TextWrapped(processState);
            }
        }

        /// <summary>
        /// Initiates the download of the tools ZIP.
        /// </summary>
        private void StartDownloadTools()
        {
            isDownloadingTools = true;
            downloadProgress = "0%";

            Task.Run(async () =>
            {
                try
                {
                    await ToolLoader.InitializeToolsAsync((progress) =>
                    {
                        downloadProgress = progress;
                        // Request UI update on the main thread
                    }, plugin);
                }
                catch (Exception ex)
                {
                    processState = $"Error downloading tools: {ex.Message}";
                    Plugin.Log.Error($"Error downloading tools: {ex.Message}");
                }
                finally
                {
                    isDownloadingTools = false;
                }
            });
        }

        /// <summary>
        /// Asynchronous method to download audio, convert it, and build an SCD file.
        /// </summary>
        private async Task DownloadConvertAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(youtubeLink))
                {
                    processState = "No YouTube link provided.";
                    Plugin.Log.Warning("No YouTube link provided.");
                    return;
                }

                processState = "Preparing download...";
                await Task.Yield(); // Ensure the UI is not blocked

                ResourceChecker.CheckDJ(plugin);

                // Define target paths
                var random = new Random();
                int rand = random.Next(9999);
                int rand2 = random.Next(9999);

                string tempMp3 = Path.Combine(Path.GetTempPath(), $"{rand}_{saveName}_{rand2}.mp3");
                string finalOgg = Path.Combine(Path.GetTempPath(), $"{rand}_{saveName}_{rand2}.ogg");
                string scdPath = Path.Combine(plugin.Configuration.DJPath, "custom", $"{rand}_{saveName}_{rand2}.scd");

                string samplePath = string.Empty;

                if (plugin.Configuration.Choice == 0)
                {
                    samplePath = Path.Combine(Configuration.Resources, "test.scd");
                } else
                {
                    samplePath = Path.Combine(Configuration.Resources, $"test{plugin.Configuration.Choice}.scd");
                }            
                
                string sampleOggPath = Path.Combine(Configuration.Resources, "7957_kids bitte funnktionier_4072.ogg");

                processState = "Updating Tools...";
                await Task.Run(async () =>
                {
                    await YoutubeDownloader.UpdateYT();
                });

                // 1) Download audio using yt-dlp
                processState = "Downloading audio (yt-dlp)...";
                Plugin.Log.Information($"Downloading {youtubeLink} -> {tempMp3}");
                await Task.Run(async () =>
                {
                    await YoutubeDownloader.DownloadAudioAsync(youtubeLink, tempMp3, useMp3: true);
                });

                // 2) Convert MP3 to OGG using ffmpeg
                processState = "Converting MP3 to OGG (ffmpeg)...";
                Plugin.Log.Information($"Converting MP3 to OGG: {tempMp3} -> {finalOgg}");
                await Task.Run(() =>
                {
                    AudioConverter.ConvertToOgg44100(tempMp3, finalOgg);
                });

                // 3) Build SCD file
                processState = "Building SCD file...";
                Plugin.Log.Information($"Building SCD: {samplePath} + {sampleOggPath} + {finalOgg} -> {scdPath}");
                await Task.Run(() =>
                {
                    Builder.BuildNewScd(samplePath, sampleOggPath, finalOgg, scdPath);
                });

                // Final message and import
                processState = "Done.";
                DJImporter.Import(plugin, saveName, $"{rand}_{saveName}_{rand2}.scd", playlist);
                plugin.RefreshMods();
            }
            catch (Exception ex)
            {
                processState = $"Error: {ex.Message}";
                Plugin.Log.Error($"Error in DownloadConvertAsync: {ex.Message}");
            }
        }
    }
}
