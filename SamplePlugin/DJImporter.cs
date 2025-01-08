using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace SamplePlugin
{
    public static class DJImporter
    {
        /// <summary>
        /// Import-Methode:
        /// 1) Sucht alle JSON-Dateien im Format "group_*_*.json" im DJ-Verzeichnis.
        /// 2) Wenn keine "custom"-Datei gefunden wird, erstellt sie eine neue "group_{next}_custom.json".
        ///    Dabei wird {next} = Anzahl vorhandener group_*_*.json Dateien + 1, startend bei 1.
        /// 3) Lädt die Ziel-JSON-Datei, fügt einen neuen Eintrag hinzu und speichert sie.
        /// </summary>
        /// <param name="plugin">Das Plugin-Objekt (für Konfiguration und Logging)</param>
        /// <param name="name">Name des neuen Options-Eintrags</param>
        /// <param name="filename">Dateiname, der unter "custom\..." gelinkt wird</param>
        public static void Import(Plugin plugin, string name, string filename, string playlistName)
        {
            var dir = plugin.Configuration.DJPath;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Plugin.Log.Error($"DJ Path invalid: {dir}");
                return;
            }

            // 1) Suche alle Dateien vom Muster "group_*_*.json"
            var allGroupFiles = Directory.GetFiles(dir, "group_*_*.json", SearchOption.TopDirectoryOnly);

            // 2) Prüfe, ob schon eine Datei mit "custom" im Namen existiert
            var customFiles = allGroupFiles.Where(f => f.Contains(playlistName, StringComparison.OrdinalIgnoreCase)).ToArray();

            string targetFile;
            if (customFiles.Length == 0)
            {
                // Keine "custom"-Datei gefunden, erstelle eine neue
                int nextNumber = allGroupFiles.Length + 1; // Startet bei 1
                var newFileName = $"group_{nextNumber:D2}_{playlistName}.json"; // z.B. group_01_custom.json
                targetFile = Path.Combine(dir, newFileName);

                // Erstelle eine neue JSON-Struktur (ANHANG1)
                var newData = CreateDefaultCustomJson(playlistName);
                SaveDJJson(plugin, targetFile, newData);

                Plugin.Log.Information($"Created new custom group file: {targetFile}");
            }
            else
            {
                // "custom"-Datei gefunden, benutze die erste gefundene
                targetFile = customFiles[0];
                Plugin.Log.Information($"Using existing custom group file: {targetFile}");
            }

            // 3) Lade die bestehende (oder neu erstellte) JSON
            var djData = LoadDJJson(plugin, targetFile);
            if (djData == null)
            {
                Plugin.Log.Error($"Failed to load or parse {targetFile}");
                return;
            }

            // 4) Erzeuge einen neuen Options-Eintrag (ANHANG2)
            var newOption = new OptionItem
            {
                Name = name,
                Description = "",
                Files = new Dictionary<string, string>
                {
                    ["sound/lolo.scd"] = Path.Combine("custom", filename).Replace("\\", "/")
                },
                FileSwaps = new Dictionary<string, string>(),
                Manipulations = new List<object>()
            };

            // 5) Füge den neuen Eintrag hinzu, ohne alte Einträge zu entfernen
            djData.Options.Add(newOption);

            // 6) Speichere die geänderte JSON zurück
            SaveDJJson(plugin, targetFile, djData);

            Plugin.Log.Information($"Added new custom option \"{name}\" to {targetFile}");
        }

        /// <summary>
        /// Erstellt die Standardstruktur für eine neue "custom"-JSON-Datei (ANHANG1).
        /// </summary>
        /// <returns>Ein neues DJJson-Objekt mit einem "Omit"-Eintrag</returns>
        private static DJJson CreateDefaultCustomJson(string playlistName)
        {
            return new DJJson
            {
                Version = 0,
                Name = playlistName,
                Description = "Custom added songs.",
                Image = "",
                Page = 0,
                Priority = 3,
                Type = "Single",
                DefaultSettings = 0,
                Options = new List<OptionItem>
                {
                    // Behalte "Omit" als ersten Eintrag
                    new OptionItem
                    {
                        Name = "Omit",
                        Description = "",
                        Files = new Dictionary<string, string>(),
                        FileSwaps = new Dictionary<string, string>(),
                        Manipulations = new List<object>()
                    }
                }
            };
        }

        /// <summary>
        /// Lädt eine JSON-Datei in ein DJJson-Objekt.
        /// </summary>
        /// <param name="plugin">Das Plugin-Objekt (für Logging)</param>
        /// <param name="path">Der Pfad zur JSON-Datei</param>
        /// <returns>Das geladene DJJson-Objekt oder null bei Fehlern</returns>
        private static DJJson? LoadDJJson(Plugin plugin, string path)
        {
            try
            {
                var jsonText = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<DJJson>(jsonText, JsonOptions());
                return data;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error loading JSON file {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Speichert das DJJson-Objekt in eine JSON-Datei.
        /// </summary>
        /// <param name="plugin">Das Plugin-Objekt (für Logging)</param>
        /// <param name="path">Der Pfad zur JSON-Datei</param>
        /// <param name="data">Das DJJson-Objekt, das gespeichert werden soll</param>
        private static void SaveDJJson(Plugin plugin, string path, DJJson data)
        {
            try
            {
                var jsonText = JsonSerializer.Serialize(data, JsonOptions(writeIndent: true));
                File.WriteAllText(path, jsonText);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error saving JSON file {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Definiert die JSON-Serializer-Optionen.
        /// </summary>
        /// <param name="writeIndent">Gibt an, ob das JSON formatiert werden soll</param>
        /// <returns>Ein JsonSerializerOptions-Objekt</returns>
        private static JsonSerializerOptions JsonOptions(bool writeIndent = false)
        {
            return new JsonSerializerOptions
            {
                WriteIndented = writeIndent,
                PropertyNamingPolicy = null, // Verwende die Property-Namen direkt (PascalCase)
                // Optional: AllowTrailingCommas, ReadCommentHandling etc.
            };
        }
    }

    /// <summary>
    /// Entspricht der JSON-Struktur der "custom"-Dateien.
    /// </summary>
    public class DJJson
    {
        public int Version { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public int Page { get; set; }
        public int Priority { get; set; }
        public string Type { get; set; } = "";
        public int DefaultSettings { get; set; }

        public List<OptionItem> Options { get; set; } = new();
    }

    /// <summary>
    /// Entspricht den Einträgen im "Options"-Array der JSON-Datei.
    /// </summary>
    public class OptionItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        public Dictionary<string, string> Files { get; set; } = new();

        public Dictionary<string, string> FileSwaps { get; set; } = new();
        public List<object> Manipulations { get; set; } = new();
    }
}
