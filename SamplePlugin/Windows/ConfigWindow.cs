using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.IO;
using System.Numerics;

namespace YTImport.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        public ConfigWindow(Plugin plugin)
            : base("YT Import Config",
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
            ImGui.Text("To set your DJ path:");
            ImGui.BulletText("Open Penumbra");
            ImGui.BulletText("Go to [Yue's + Lu's] Dj -> Edit Mod");
            ImGui.BulletText("Click 'Open Mod Directory'");
            ImGui.BulletText("Copy the complete directory path");
            ImGui.BulletText("Paste it here:");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Aktuellen Pfad aus der Config holen
            var pathInput = plugin.Configuration.DJPath ?? string.Empty;

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
                        plugin.Configuration.DJPath = fullPath;
                    }
                    else
                    {
                        // Wenn der User alles löscht, kann man entscheiden:
                        plugin.Configuration.DJPath = string.Empty;
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

            // Button zum Schließen
            if (ImGui.Button("Close Config"))
            {
                this.IsOpen = false;
            }
        }
    }
}
