using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.System.File;
using ImGuiNET;
using System;
using System.IO;
using Soundy.FileAnalyzer;
using System.Numerics;
using Dalamud.Utility;
using System.Threading.Tasks;

namespace Soundy.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        // Oben in ConfigWindow
        private Task? fixTask = null;   // aktuelle Task
        private string fixStatus = "";     // Text fürs UI
        private float fakeProgress = 0f;     // rein optisch


        public ConfigWindow(Plugin plugin)
            : base("Soundy Config",
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            // Festlegen von Minimal- und Maximalgröße
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 200),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };

            TitleBarButtons.Add(Support.NavBarBtn);

            this.plugin = plugin;
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            Support.DrawRight();

            // Kurze Anleitung
            ImGui.Text("Your Penumbra Mod Path:");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Aktuellen Pfad aus der Config holen
            var pathInput = plugin.Configuration.PenumbraPath ?? string.Empty;

            // Eingabefeld
            if (ImGui.InputText("Directory Path##cfg", ref pathInput, 1024))
            {
                // Versuchen, den Pfad zu normalisieren/validieren
                try
                {
                    // Ein leerer String wirft ggf. eine Ausnahme, daher abfangen
                    if (!string.IsNullOrWhiteSpace(pathInput))
                    {
                        // Normalisiert z. B. "..", ".", etc.
                        var fullPath = Path.GetFullPath(pathInput);
                        plugin.Configuration.PenumbraPath = fullPath;
                    }
                    else
                    {
                        // Wenn der User alles löscht, kann man entscheiden:
                        plugin.Configuration.PenumbraPath = string.Empty;
                    }
                }
                catch (Exception e)
                {
                    // Optional: Fehlermeldung loggen oder ignorieren
                    Plugin.Log.Error($"Invalid path: {e.Message}");
                    // Pfad bleibt dann unverändert
                }

                // Speichern der Config
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            if (ImGui.Button("Get Penumbra Path"))
            {
                FileAnalyzer.FileManager.PenumbraChecker(plugin);
            }

            if (!plugin.Configuration.PenumbraPath.IsNullOrEmpty())
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Fix renamed Mods"))
                {
                    // Mehrfachklick verhindern
                    if (fixTask == null || fixTask.IsCompleted)
                    {
                        fixStatus = "Fix is running …";
                        fakeProgress = 0f;

                        fixTask = Task.Run(async () =>
                        {
                            try
                            {
                                // eigentliche Arbeit
                                await PathHelpers.RunOneShotFixAsync(plugin.Configuration.PenumbraPath);
                                plugin.RefreshMods();

                                // Erfolg
                                fixStatus = "Fix done";
                            }
                            catch (Exception ex)
                            {
                                fixStatus = $"Error: {ex.Message}";
                            }
                        });
                    }
                }

                ImGui.Spacing();

                // ganz unten in Draw(), noch vor „Close Config“:
                if (!string.IsNullOrEmpty(fixStatus))
                {
                    if (fixTask != null && !fixTask.IsCompleted)
                    {
                        // simple Fake-Progress (schiebt Balken langsam nach rechts)
                        fakeProgress = Math.Min(fakeProgress + ImGui.GetIO().DeltaTime * 0.25f, 0.9f);
                        ImGui.ProgressBar(fakeProgress, new Vector2(-1, 0));
                    }

                    ImGui.TextWrapped(fixStatus);
                }

            }


            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            // Button zum Schließen
            if (ImGui.Button("Close Config"))
            {
                this.IsOpen = false;
            }
        }
    }
}
