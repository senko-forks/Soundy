using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ECommons.DalamudServices;
using Soundy.FileAnalyzer;
using Soundy.Pap;
using System.Collections.Generic;
using System.Linq;
using Soundy.Scd;
using Dalamud.Interface;
using static Soundy.Pap.PapManager;
using System.ComponentModel.DataAnnotations;
using System.Formats.Tar;
using ECommons;
using Lumina.Excel.Sheets;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using System.Diagnostics.Metrics;
using ECommons.ImGuiMethods;

namespace Soundy.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        // Input fields
        private string saveName = "";
        private string youtubeLink = "";
        private string playlistName = ""; // used for new playlist input

        // Playlist selection
        private List<string> availablePlaylists = new();
        private bool playlistsLoaded = false;
        private int selectedPlaylistIndex = 0;
        private bool createNewPlaylist = false;

        // Status messages
        private string processState = "";

        // Download status and progress
        private bool isDownloadingTools = false;
        private string downloadProgress = "";

        // Mod-Auswahl
        private string? selectedMod = null;
        private string modFilter = "";
        private List<string> modFolders = new();

        // -----------------------------
        // NEU: PAP-Auswahl
        // -----------------------------
        /// <summary>Die intern gescannte Liste aller gefundenen PAPs, inkl. extrahierter SCD-Details.</summary>
        private List<GroupedPapEntry> papEntries = new();
        /// <summary>Wurde die PAP-Liste schon einmal geladen?</summary>
        private bool papListLoaded = false;
        /// <summary>Filtertext fürs UI.</summary>
        private string papFilter = "";
        /// <summary>Die vom User selektierten PAP-Einträge.</summary>
        private List<GroupedPapEntry> userSelectedPapEntries = new();

        // Tracking ongoing tasks
        private Task? currentDownloadTask;
        private Task<List<GroupedPapEntry>>? papScanTask;
        private Task<List<string>>? playlistLoadTask;

        private bool showPapHelp = false;

        // Step-based progress for import process
        private readonly string[] importSteps = new[]
        {
            "Preparing download...",
            "Updating Tools...",
            "Downloading audio...",
            "Building SCD file...",
            "Injecting sound...",
            "Updating JSON...",
            "Done."
        };
        private int currentStep = -1;

        public MainWindow(Plugin plugin)
            : base("Soundy", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(850, 600),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            TitleBarButtons.Add(Support.NavBarBtn);
            TitleBarButtons.Add(Support.DiscordBtn);
            this.Size = new Vector2(800, 500);

            FileManager.PenumbraChecker(plugin);
        }

        public void Dispose() { }

        public override void Draw()
        {
            // 1) Tools-Download-Check
            if (!plugin.Configuration.AreToolsDownloaded)
            {
                DrawDownloadToolsUi();
                return;
            }

            // 2) Tool verification
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

            // 3) Penumbra path check
            if (string.IsNullOrEmpty(plugin.Configuration.PenumbraPath))
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Please set your Penumbra Mod Path in the settings.");
                if (ImGui.Button("Show Settings"))
                {
                    plugin.ToggleConfigUI();
                }
                return;
            }

            // 4) Mod-Auswahl
            if (string.IsNullOrWhiteSpace(selectedMod))
            {
                DrawModSelectionUi();
                if (ImGui.Button("Discord Server"))
                {
                    GenericHelpers.ShellStart("https://discord.gg/2CbzecNZav");
                }
                if (ImGui.Button("Open Config"))
                {
                    plugin.ToggleConfigUI();
                }
                ImGui.PushStyleColor(ImGuiCol.Text, ColorHelpers.Vector(System.Drawing.KnownColor.Orange));
                ImGui.TextWrapped("If you encounter any issues, feel free to report them on our Discord server.");
                ImGui.PopStyleColor();

                return;
            }
            else
            {
                ImGui.Text($"Selected Mod: {selectedMod}");
                if (ImGui.Button("Change Mod"))
                {
                    selectedMod = null;
                    modFilter = "";
                    papEntries.Clear();
                    papListLoaded = false;
                    papScanTask = null;
                    playlistsLoaded = false;
                    playlistLoadTask = null;
                    return;
                }
                ImGui.SameLine();
                if (ImGui.Button("Reset Mod to Default"))
                {
                    var dirPath = Path.Combine(plugin.Configuration.PenumbraPath, selectedMod);
                    FileManager.ResetMod(dirPath);
                    papListLoaded = false;
                    papScanTask = null;
                    playlistsLoaded = false;
                    playlistLoadTask = null;
                    plugin.RefreshMods();
                }
                ImGui.Separator();

                if (!playlistsLoaded)
                {
                    EnsurePlaylistsLoaded();
                    if (!playlistsLoaded)
                    {
                        ImGui.Text("Loading playlists...");
                        ImGui.ProgressBar(0f, new Vector2(-1, 0));
                        return;
                    }
                }
            }

            // 5) Input fields (YouTube, SaveName, Choice)
            ImGui.InputText("YouTube Link##youtubeLink", ref youtubeLink, 1024);
            ImGui.InputText("Save Name##saveName", ref saveName, 1024);
            if (availablePlaylists.Count > 0)
            {
                ImGui.BeginDisabled(createNewPlaylist);
                ImGui.Combo("Playlist##playlistDropdown", ref selectedPlaylistIndex, availablePlaylists.ToArray(), availablePlaylists.Count);
                ImGui.EndDisabled();
            }
            else
            {
                ImGui.Text("No playlists found.");
            }
            ImGui.Checkbox("Create new Playlist##createPlaylist", ref createNewPlaylist);
            if (createNewPlaylist)
            {
                ImGui.InputText("New Playlist Name##playlistName", ref playlistName, 1024);
            }
            else if (availablePlaylists.Count > 0)
            {
                playlistName = availablePlaylists[selectedPlaylistIndex];
            }

            //if (ImGui.DragFloat("Volume##volume", ref plugin.Configuration.Volume, 0.1f, 1f, 5.0f))
            //{
            //    if (plugin.Configuration.Volume > 5.0f) plugin.Configuration.Volume = 5.0f;
            //    else if (plugin.Configuration.Volume < 0.3f) plugin.Configuration.Volume = 0.3f;
            //    plugin.Configuration.Save();
            //}

            var choice = plugin.Configuration.Choice;
            string[] choices = { "BGM", "Sound Effects", "System Sounds" };
            if (ImGui.Combo("##choice", ref choice, choices, choices.Length))
            {
                plugin.Configuration.Choice = choice;
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            // ================ PAP-Auswahl-Block ================
            if (!papListLoaded)
            {
                var dirPath = Path.Combine(plugin.Configuration.PenumbraPath, selectedMod);

                if (papScanTask == null)
                {
                    papScanTask = PapManager.ScanForPapDetailsGroupedAsync(dirPath, msg => processState = msg);
                }

                if (!papScanTask.IsCompleted)
                {
                    ImGui.Text(processState);
                    return;
                } 

                try
                {
                    papEntries = papScanTask.Result;
                }
                catch (Exception ex)
                {
                    processState = $"error in pap scan: {ex}";
                    papEntries = new List<GroupedPapEntry>();
                }

                papScanTask = null;
                papListLoaded = true;

                if (papEntries.Count == 1)
                {
                    papEntries[0].Selected = true;
                }
            }

            if (papEntries.Count == 0)
            {
                ImGui.TextColored(ColorHelpers.Vector(System.Drawing.KnownColor.Red), "This mod doesn't contain an Animation File.");
                return;
            }

            // Filter and selection helpers
            ImGui.InputText("Filter##papFilter", ref papFilter, 256);
            ImGuiEx.HelpMarker("Select the animations whose sounds should be replaced.");

            ImGui.SameLine();
            if (ImGui.Button("Clear##papFilter"))
            {
                papFilter = "";
            }
            ImGui.SameLine();
            if (ImGui.Button("Select All"))
            {
                foreach (var entry in papEntries)
                    entry.Selected = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Select None"))
            {
                foreach (var entry in papEntries)
                    entry.Selected = false;
            }
            if (ImGui.Button("Help")) showPapHelp = true;

            // Gefilterte Liste
            var visible = FilterPapEntries(papEntries, papFilter);

            // Tabelle: [Checkbox] [Options] [File Path] [SCD Path] [Actions]
            ImGui.Spacing();
            if (ImGui.BeginTable("papTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 200)))
            {
                ImGui.TableSetupColumn("Use", ImGuiTableColumnFlags.WidthFixed, 35f);
                ImGui.TableSetupColumn("Option", ImGuiTableColumnFlags.WidthFixed, 500f);
                ImGui.TableSetupColumn("SCD Path", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 55f);
                ImGui.TableHeadersRow();

                foreach (var entry in visible)
                {
                    ImGui.TableNextRow();

                    // Spalte 1: Checkbox
                    ImGui.TableSetColumnIndex(0);
                    bool sel = entry.Selected;
                    if (ImGui.Checkbox($"##chk_{entry.PapPath}", ref sel))
                    {
                        entry.Selected = sel;
                    }

                    // Spalte 2: Option Namen
                    ImGui.TableSetColumnIndex(1);
                    var optionNames = string.Join(", ", entry.References
                        .Select(r => $"{r.GroupName} -> {r.OptionName}")
                        .Distinct());
                    ImGui.TextUnformatted(string.IsNullOrEmpty(optionNames) ? "(root)" : optionNames);


                    // Spalte 4: SCD Pfad
                    ImGui.TableSetColumnIndex(2);
                    if (entry.ScdDetails.Count > 0)
                    {
                        var test = entry.ScdDetails[0].SCDPath;
                        ImGui.TextUnformatted(string.IsNullOrEmpty(entry.ScdDetails[0].SCDPath) ? "Will be generated." : test);
                    }
                    else
                    {
                        ImGui.TextUnformatted("Will be generated.");
                    }

                    // Spalte 5: Details-Button (öffnet Popup)
                    ImGui.TableSetColumnIndex(3);
                    if (ImGui.Button($"Details##btn_{entry.PapPath}"))
                    {
                        ImGui.OpenPopup($"popup_{entry.PapPath}");
                    }
                    if (ImGui.BeginPopup($"popup_{entry.PapPath}"))
                    {
                        ImGui.Text($"References for:\n{entry.PapPath}");
                        ImGui.Separator();
                        foreach (var r in entry.References)
                        {
                            ImGui.TextUnformatted($"{Path.GetFileName(r.JsonFile)} -> {r.OptionName}");
                        }
                        ImGui.EndPopup();
                    }
                }
                ImGui.EndTable();
            }


            ImGui.Spacing();

            // 7) Download & Save-Button
            if (ImGui.Button("Download & Save"))
            {
                if (currentDownloadTask == null || currentDownloadTask.IsCompleted)
                {
                    // Sammle alle vom Nutzer selektierten PAP-Einträge
                    userSelectedPapEntries = papEntries.Where(e => e.Selected).ToList();
                    if (userSelectedPapEntries.Count == 0)
                    {
                        processState = "No PAP selected. Please select a PAP or add one if none exist.";
                    }
                    else
                    {
                        currentStep = 0;
                        processState = importSteps[currentStep];
                        currentDownloadTask = DownloadConvertAsync();
                    }
                }
            }

            // Status / Meldungen
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawProcessStateUi();

            if (showPapHelp)
            {
                ImGui.OpenPopup("PapHelpPopup");
                showPapHelp = false;
            }
            bool open = true;
            ImGui.SetNextWindowSize(new Vector2(400, 150));
            if (ImGui.BeginPopupModal("PapHelpPopup", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped("In this step you choose which animation files (PAP) will have their sound replaced. Each row shows the group and option that references the animation, the PAP file path and its current SCD path. If multiple options share the same SCD path you can pick any of them.");
                ImGui.Separator();
                if (ImGui.Button("Close"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawDownloadToolsUi()
        {
            if (isDownloadingTools)
            {
                ImGui.Text($"Downloading tools... {downloadProgress}");
                float progressValue = 0.0f;
                if (double.TryParse(downloadProgress.Replace("%", ""), out double p))
                {
                    progressValue = (float)(p / 100.0);
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
        }

        private void DrawModSelectionUi()
        {
            ImGui.Text("Select a mod folder for import:");
            ImGui.Separator();
            ImGui.InputText("Filter", ref modFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button("Refresh"))
            {
                LoadModFolders();
            }
            if (!modFolders.Any())
            {
                LoadModFolders();
            }
            if (ImGui.BeginChild("ModList", new Vector2(0, 200), true))
            {
                foreach (var mod in modFolders)
                {
                    if (string.IsNullOrEmpty(modFilter) || mod.ToLower().Contains(modFilter.ToLower()))
                    {
                        if (ImGui.Selectable(mod))
                        {
                            selectedMod = mod;
                            papListLoaded = false; // Neu scannen, falls Mod gewechselt wird
                            papScanTask = null;
                            playlistsLoaded = false;
                            playlistLoadTask = null;
                        }
                    }
                }
                ImGui.EndChild();
            }
        }

        private void LoadModFolders()
        {
            modFolders.Clear();
            if (Directory.Exists(plugin.Configuration.PenumbraPath))
            {
                modFolders = Directory.GetDirectories(plugin.Configuration.PenumbraPath)
                    .Select(Path.GetFileName)
                    .ToList();
            }
        }

        private void EnsurePlaylistsLoaded()
        {
            if (playlistsLoaded || string.IsNullOrWhiteSpace(selectedMod)) return;

            var dirPath = Path.Combine(plugin.Configuration.PenumbraPath, selectedMod);
            if (!Directory.Exists(dirPath)) return;

            if (playlistLoadTask == null)
            {
                playlistLoadTask = FileManager.GetPlaylistsAsync(dirPath);
                processState = "Loading playlists...";
            }

            if (playlistLoadTask.IsCompleted)
            {
                availablePlaylists = playlistLoadTask.Result;
                playlistsLoaded = true;
                playlistLoadTask = null;
                processState = string.Empty;
                if (selectedPlaylistIndex >= availablePlaylists.Count)
                    selectedPlaylistIndex = 0;
            }
        }

        private List<GroupedPapEntry> FilterPapEntries(List<GroupedPapEntry> source, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return source;
            var low = filter.ToLowerInvariant();
            return source.Where(e =>
                e.PapPath.ToLowerInvariant().Contains(low) ||
                e.References.Any(r =>
                    r.OptionName.ToLowerInvariant().Contains(low) ||
                    r.JsonFile.ToLowerInvariant().Contains(low)) ||
                e.ScdDetails.Any(d =>
                    d.SCDPath.ToLowerInvariant().Contains(low) ||
                    d.AnimationName.ToLowerInvariant().Contains(low) ||
                    d.ActorName.ToLowerInvariant().Contains(low))
            ).ToList();
        }

        public void ChangeState(string change, bool newStep = false)
        {
            processState = change;
            if (newStep) currentStep++;
        }

        private bool IsBusy()
        {
            if (isDownloadingTools)
                return true;
            if (currentDownloadTask != null && !currentDownloadTask.IsCompleted)
                return true;
            if (papScanTask != null && !papScanTask.IsCompleted)
                return true;
            if (playlistLoadTask != null && !playlistLoadTask.IsCompleted)
                return true;
            return false;
        }

        private void DrawProcessStateUi()
        {
            if (string.IsNullOrEmpty(processState))
                return;

            ImGui.BeginChild("ProcessState", new Vector2(0, 50), true);

            var color = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            if (processState.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                color = ColorHelpers.Vector(System.Drawing.KnownColor.Red);
            else if (processState.StartsWith("Done", StringComparison.OrdinalIgnoreCase))
                color = ColorHelpers.Vector(System.Drawing.KnownColor.LawnGreen);

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextWrapped(processState);
            ImGui.PopStyleColor();

            if (currentStep >= 0 && importSteps.Length > 1)
            {
                float value = (float)currentStep / (importSteps.Length - 1);
                ImGui.ProgressBar(value, new Vector2(-1, 0));
            }

            ImGui.EndChild();
        }

        /// <summary>
        /// Asynchronous method to download audio, convert it, build a new SCD file,
        /// and for each selected PAP either update the existing SCD or inject a new one.
        /// Anschließend wird in den JSONs der mod-spezifische Song-Eintrag aktualisiert.
        /// </summary>
        private async Task DownloadConvertAsync()
        {
            try
            {
                // 1) YouTube-Link validieren
                if (string.IsNullOrWhiteSpace(youtubeLink))
                {
                    currentStep = -1;
                    processState = "No YouTube link provided.";
                    Plugin.Log.Warning(processState);
                    return;
                }
                if (userSelectedPapEntries == null || userSelectedPapEntries.Count == 0)
                {
                    currentStep = -1;
                    processState = "No PAP selected (userSelectedPapEntries is empty).";
                    return;
                }
                var dirPath = Path.Combine(plugin.Configuration.PenumbraPath, selectedMod);
                FileManager.ScanForSoundyDir(dirPath);

                var random = new Random();
                int rand = random.Next(9999);
                int rand2 = random.Next(9999);
                currentStep = 0;
                processState = importSteps[currentStep];
                await Task.Yield();
                ResourceChecker.CheckDJ(plugin);

                string tempMp3 = Path.Combine(Path.GetTempPath(), $"{rand}_{saveName}_{rand2}.wav");
                string finalOgg = Path.Combine(Path.GetTempPath(), $"{rand}_{saveName}_{rand2}.ogg");

                // Erzeugen eines neuen SCD-Files
                string scdPath = Path.Combine(dirPath, "soundy", "songs",
                    $"{rand}_{saveName}_{rand2}.scd");

                string samplePath;
                if (plugin.Configuration.Choice == 0)
                {
                    samplePath = Path.Combine(Configuration.Resources, "test.scd");
                }
                else
                {
                    samplePath = Path.Combine(Configuration.Resources, $"test{plugin.Configuration.Choice}.scd");
                }

                currentStep = 1;
                processState = importSteps[currentStep];
                await Task.Run(async () =>
                {
                    await YoutubeDownloader.UpdateYT();
                });

                // 2) Audio downloaden
                currentStep = 2;
                processState = importSteps[currentStep];

                Plugin.Log.Information($"Downloading {youtubeLink} -> {tempMp3}");
                await Task.Run(async () =>
                {
                    //await YoutubeDownloader.DownloadAudioAsync(youtubeLink, tempMp3, useMp3: true);
                    await YoutubeDownloadAndConvert.DownloadAndConvertAsync(
                        youtubeUrl: youtubeLink,
                        outputOggFile: finalOgg,
                        userVolume: 1.0f,   // -> ~2.0-Faktor => +6 dB
                        applyLimiter: true, // Verhindert hartes Clipping
                        quality: 5,         // Höchste Vorbis-Qualität
                        main: this
                    );
                });

                // 3) Audio konvertieren

                //try
                //{
                //    await Task.Run(() =>
                //    {
                //        AudioConverter.ConvertToOgg44100(tempMp3, finalOgg, plugin.Configuration.Volume);
                //    });
                //}
                //catch (Exception ex)
                //{
                //    processState = $"FFmpeg failed or timed out: {ex.Message}";
                //    Plugin.Log.Error($"FFmpeg error: {ex.Message}");
                //    return;
                //}

                // 4) Neues SCD-File erstellen
                currentStep = 3;
                processState = importSteps[currentStep];
                Plugin.Log.Information($"Building SCD: {samplePath} + {finalOgg} -> {scdPath}");
                try
                {
                    ScdEdit.CreateScd(samplePath, scdPath, finalOgg);
                }
                catch (Exception ex)
                {
                    currentStep = -1;
                    processState = $"Error with creating scd: {ex}";
                    return;
                }

                // 5) Für jeden ausgewählten PAP-Eintrag:
                //    - Falls bereits ein SCD in der PAP vorhanden ist, wird dieser mit dem neuen SCD überschrieben.
                //    - Andernfalls wird der neue SCD in die PAP injiziert.
                currentStep = 4;
                processState = importSteps[currentStep];
                List<string> finalSCDPaths = new List<string>();
                Dictionary<string, string> replacements = new();
                var count = -1;
                foreach (var papEntry in userSelectedPapEntries)
                {
                    count++;
                    // Erstelle einen absoluten Pfad für die PAP-Datei
                    string currentPapPath = Path.Combine(dirPath, papEntry.PapPath);
                    if (!File.Exists(currentPapPath))
                    {
                        Plugin.Log.Warning($"PAP not found: {currentPapPath}");
                        continue;
                    }
                    string newPap = Path.Combine("soundy", "paps", $"injected_{Path.GetFileName(papEntry.PapPath)}");
                    string newPapPath = Path.Combine(dirPath, newPap);
                    if (papEntry.ScdDetails.Count < 1)
                    {
                        // Überschreibe den vorhandenen SCD in der PAP
                        // Hier rufen wir PapInjector.InjectSound auf – in diesem Beispiel nehmen wir an,
                        // dass das Überschreiben im selben File (currentPapPath) erfolgt.
                        string customRoute = $"soundy/sounds/{rand}_{rand2}.scd";

                        try
                        {
                            await Svc.Framework.RunOnTick(() =>
                            {
                                PapInjector.InjectSound(customRoute, currentPapPath, newPapPath);
                            });
                        }
                        catch (Exception ex)
                        {
                            currentStep = -1;
                            processState = $"Error in Injecting: {ex.Message}";
                            return;
                        }

                        replacements.Add(papEntry.PapPath, newPap);

                        finalSCDPaths.Add(customRoute);
                    }
                    else
                    {
                        // Es gibt keinen SCD in der PAP – injiziere den neuen Sound.
                        // Bestimme einen neuen Pfad für die aktualisierte PAP (z.B. in "soundy/paps")
                        //PapInjector.InjectSound(scdPath, currentPapPath, newPapPath);
                        foreach (var scd in papEntry.ScdDetails)
                        {
                            finalSCDPaths.Add(scd.SCDPath);
                        }
                    }

                }

                if (replacements.Count > 0)
                    FileManager.ReplacePaps(dirPath, replacements);

                // 6) JSON aktualisieren: Es wird für jeden PAP-Eintrag der neue SCD-Pfad eingetragen.
                currentStep = 5;
                processState = importSteps[currentStep];
                Plugin.Log.Information($"AddSong: {finalSCDPaths.Count} sscdPaths -> {scdPath}");
                try
                {
                    FileManager.AddSong(dirPath, finalSCDPaths, scdPath, saveName, playlistName.Trim());
                }
                catch (Exception ex)
                {
                    currentStep = -1;
                    processState = $"Error in AddSong: {ex.Message}";
                    return;
                }

                currentStep = 6;
                processState = importSteps[currentStep];
                papListLoaded = false;
                papScanTask = null;
                playlistsLoaded = false;
                playlistLoadTask = null;
                plugin.RefreshMods();
            }
            catch (Exception ex)
            {
                currentStep = -1;
                processState = $"Error: {ex.Message}";
                Plugin.Log.Error($"Error in DownloadConvertAsync: {ex.Message}");
            }
        }

        private void StartDownloadTools()
        {
            isDownloadingTools = true;
            downloadProgress = "0%";
            Task.Run(async () =>
            {
                try
                {
                    await ToolLoader.InitializeToolsAsync(progress =>
                    {
                        downloadProgress = progress;
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
    }
}
