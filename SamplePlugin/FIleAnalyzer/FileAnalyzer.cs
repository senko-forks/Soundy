using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using ECommons;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.FileLayerGroupLayerFilter;

namespace Soundy.FileAnalyzer
{
    public class FileManager
    {
        public static List<GroupedScdEntry> ScanForScdDetailsGrouped(string dirPath)
        {
            // Temporäre Liste für Einzelfunde
            var rawList = new List<(string JsonFile, string OptionName, string ScdPath)>();

            var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
                    if (root == null) continue;

                    // 1) root.Files
                    if (root.Files != null)
                    {
                        foreach (var key in root.Files.Keys)
                        {
                            if (key.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                            {
                                rawList.Add((file, "(root)", key));
                            }
                        }
                    }

                    // 2) root.Options
                    if (root.Options != null)
                    {
                        foreach (var opt in root.Options)
                        {
                            if (opt.Files == null) continue;
                            foreach (var key in opt.Files.Keys)
                            {
                                if (key.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                                {
                                    rawList.Add((file, opt.Name ?? "(no name)", key));
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Falls eine JSON "kaputt" ist, ignorieren wir das
                }
            }

            // Jetzt gruppieren wir nach SCD-Pfad:
            // => 1 Group pro SCD-Datei; darin alle Referenzen (JSON-File + OptionName).
            var groups = rawList
                .GroupBy(x => x.ScdPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GroupedScdEntry
                {
                    ScdPath = g.Key,
                    References = g.Select(x => new ScdReference
                    {
                        JsonFile = x.JsonFile,
                        OptionName = x.OptionName
                    }).ToList()
                })
                .ToList();

            return groups;
        }


        /// <summary>
        /// ScanForScd:
        /// Sucht im Hauptverzeichnis (dirPath) nach einer .json-Datei und liefert den ersten gefundenen scd‑Route (Key) aus dem Files‑Objekt.
        /// (Siehe Anhang 1.)
        /// </summary>
        public static List<string> ScanForScd(string dirPath)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
                    if (root?.Options != null)
                    {
                        foreach (var opt in root.Options)
                        {
                            if (opt.Files != null)
                            {
                                // Der Schlüssel ist hier die scd-Route
                                foreach (var key in opt.Files.Keys)
                                {
                                    if (key.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                                    {
                                        result.Add(key);
                                    }
                                }
                            }
                        }
                    }
                    if (root?.Files != null)
                    {
                        foreach (var key in root.Files.Keys)
                        {
                            if (key.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(key);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignorieren, wenn eine Datei nicht passt.
                }
            }
            return result.ToList();
        }

        /// <summary>
        /// ScanForPap:
        /// Sucht im Hauptverzeichnis (dirPath) in allen .json-Dateien nach allen .pap‑Routen.
        /// Liefert eine Liste von eindeutigen (d.h. ohne Dopplungen) Schlüsseln.
        /// </summary>
        public static List<string> ScanForPap(string dirPath)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
                    if (root?.Options != null)
                    {
                        foreach (var opt in root.Options)
                        {
                            if (opt.Files != null)
                            {
                                foreach (var val in opt.Files.Values)
                                {
                                    if (val.EndsWith(".pap", StringComparison.OrdinalIgnoreCase))
                                    {
                                        result.Add(val);
                                    }
                                }
                            }
                        }
                    }
                    if (root?.Files != null)
                    {
                        foreach (var val in root.Files.Values)
                        {
                            if (val.EndsWith(".pap", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(val);
                            }
                        }
                    }
                }
                catch { }
            }
            return result.ToList();
        }

        /// <summary>
        /// ScanForSoundyDir:
        /// Überprüft, ob im angegebenen Verzeichnis (dirPath) ein Ordner "soundy" existiert
        /// – sowie die Unterordner "soundy/songs" und "soundy/paps".
        /// Fehlt einer, wird er erstellt.
        /// </summary>
        public static void ScanForSoundyDir(string dirPath)
        {
            string soundyDir = Path.Combine(dirPath, "soundy");
            if (!Directory.Exists(soundyDir))
                Directory.CreateDirectory(soundyDir);

            string songsDir = Path.Combine(soundyDir, "songs");
            if (!Directory.Exists(songsDir))
                Directory.CreateDirectory(songsDir);

            string papsDir = Path.Combine(soundyDir, "paps");
            if (!Directory.Exists(papsDir))
                Directory.CreateDirectory(papsDir);
        }

        /// <summary>
        /// AddSong:
        /// Sucht im Hauptverzeichnis nach einer JSON-Datei, die auf *_soundy.json endet.
        /// Falls keine gefunden wird, wird eine neue Datei "group_099_soundy.json" mit einem Standardtemplate erzeugt.
        /// Anschließend wird ein neuer Song-Eintrag (Option) mit den angegebenen Daten hinzugefügt.
        /// </summary>
        public static void AddSong(string dirPath, List<string> scdRoute, string newSong, string newSongName)
        {
            // 1) JSON-Datei suchen
            var soundyJsonFiles = Directory.GetFiles(dirPath, "*_soundy.json", SearchOption.TopDirectoryOnly);
            SoundyJsonRoot soundyJson;
            string targetFile;

            // 2) Falls keine Soundy-Datei existiert, legen wir eine neue an:
            if (soundyJsonFiles.Length == 0)
            {
                var groupJsons = Directory.GetFiles(dirPath, "group_*.json", SearchOption.TopDirectoryOnly);
                int index = groupJsons.Length + 1;

                // "D3" bedeutet "Format als Dezimalzahl, mit 3 Ziffern und voran­gestellten 0"
                string indexPadded = index.ToString("D3");

                // Dann bauen wir den Dateinamen zusammen:
                targetFile = Path.Combine(dirPath, $"group_{indexPadded}_soundy.json");

                // Standard-Template
                soundyJson = new SoundyJsonRoot
                {
                    Version = "1.0.0",
                    Name = "Soundy",
                    Description = "Imported Tracks.",
                    Priority = 9999,
                    Type = "Single",
                    DefaultSettings = 0,
                    Options = new List<Option>
                    {
                        // wir haben initial nur eine Off-Option:
                        new Option
                        {
                            Name = "Off",
                            Description = "",
                            Files = new Dictionary<string, string>(),
                            FileSwaps = new Dictionary<string, string>(),
                            Manipulations = new List<object>()
                        }
                    }
                };
            }
            else
            {
                // Soundy-File laden
                targetFile = soundyJsonFiles[0];
                string json = File.ReadAllText(targetFile);
                soundyJson = JsonConvert.DeserializeObject<SoundyJsonRoot>(json)
                    ?? new SoundyJsonRoot
                    {
                        Version = "1.0.0",
                        Name = "Soundy",
                        Description = "Imported Tracks.",
                        Priority = 9999,
                        Type = "Single",
                        DefaultSettings = 0,
                        Options = new List<Option>
                        {
                            new Option
                            {
                                Name = "Off",
                                Description = "",
                                Files = new Dictionary<string, string>(),
                                FileSwaps = new Dictionary<string, string>(),
                                Manipulations = new List<object>()
                            }
                        }
                    };

                // **WICHTIG**: Nach dem Deserialisieren jede Files-Collection
                // durch eine "frische" Dictionary-Instanz ersetzen.
                if (soundyJson.Options != null)
                {
                    foreach (var opt in soundyJson.Options)
                    {
                        var oldDict = opt.Files ?? new Dictionary<string, string>();
                        opt.Files = new Dictionary<string, string>(oldDict);
                    }
                }
            }

            // 3) Neue Einträge anlegen (Song-Files füllen)
            var files = new Dictionary<string, string>();
            var newSongRel = PathHelpers.ToRelative(dirPath, newSong);
            foreach (var route in scdRoute)
            {
                files[route] = newSongRel;
            }

            // Neuer Song-Eintrag
            var newOption = new Option
            {
                Name = newSongName,
                Description = "",
                Files = files,
                FileSwaps = new Dictionary<string, string>(),
                Manipulations = new List<object>()
            };

            // 4) Neue Option anhängen
            soundyJson.Options.Add(newOption);

            // 5) JSON abspeichern
            string updatedJson = JsonConvert.SerializeObject(soundyJson, Formatting.Indented);
            Plugin.Log.Information($"Writing Soundy JSON to {targetFile} with {soundyJson.Options.Count} options.");
            File.WriteAllText(targetFile, updatedJson);
        }


        public static void PenumbraChecker(Plugin plugin)
        {
            // Hole den Konfigurationsordner aus dem Plugin-Interface.
            var configDir = Plugin.PluginInterface.ConfigDirectory.FullName;
            // Gehe einen Ordner zurück:
            string? parentDir = Directory.GetParent(configDir)?.FullName;
            if (string.IsNullOrEmpty(parentDir))
            {
                return;
            }

            // Baue den Pfad zur Penumbra.json im Parent-Ordner
            string penumbraJsonPath = Path.Combine(parentDir, "Penumbra.json");
            if (!File.Exists(penumbraJsonPath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(penumbraJsonPath);
                var penumbraConfig = JsonConvert.DeserializeObject<PenumbraJson>(json);
                if (penumbraConfig != null && !string.IsNullOrWhiteSpace(penumbraConfig.ModDirectory))
                {
                    plugin.Configuration.PenumbraPath = penumbraConfig.ModDirectory;
                    plugin.Configuration.Save();
                }
            }
            catch (Exception ex)
            {
                // ggf. Logging
            }
        }

        /// <summary>
        /// ReplacePaps:
        /// In allen .json-Dateien im Hauptverzeichnis werden in den Files-Dictionaries alle Pfade, 
        /// die im Dictionary <paramref name="paps"/> (oldPath->newPath) vorkommen, ersetzt.
        /// Zusätzlich werden diese Ersetzungen in "soundy/replacements.json" mitgeloggt, um sie
        /// über <see cref="ResetMod"/> zurücksetzen zu können.
        /// </summary>
        public static void ReplacePaps(string dirPath, Dictionary<string, string> paps)
        {
            // Erstelle oder lade das Replacements-Dictionary aus soundy/replacements.json
            string soundyDir = Path.Combine(dirPath, "soundy");
            if (!Directory.Exists(soundyDir))
                Directory.CreateDirectory(soundyDir);

            string replacementsFile = Path.Combine(soundyDir, "replacements.json");
            Dictionary<string, string> replacements;

            if (File.Exists(replacementsFile))
            {
                try
                {
                    var content = File.ReadAllText(replacementsFile);
                    replacements = JsonConvert.DeserializeObject<Dictionary<string, string>>(content)
                                   ?? new Dictionary<string, string>();
                }
                catch
                {
                    replacements = new Dictionary<string, string>();
                }
            }
            else
            {
                replacements = new Dictionary<string, string>();
            }

            // Durchsuche alle JSON-Dateien im Hauptverzeichnis
            var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                // Falls es die replacements.json selbst ist, überspringen
                if (file.EndsWith("replacements.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    string json = File.ReadAllText(file);
                    var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
                    if (root == null)
                        continue;

                    bool modified = false;

                    // 1) root.Files
                    if (root.Files != null)
                    {
                        var keys = root.Files.Keys.ToList();
                        foreach (var key in keys)
                        {
                            var oldVal = root.Files[key];
                            if (!string.IsNullOrEmpty(oldVal) && paps.ContainsKey(oldVal))
                            {
                                var newVal = paps[oldVal];
                                root.Files[key] = newVal;
                                modified = true;

                                // ins replacements-Dictionary eintragen: newVal -> oldVal
                                // nur wenn es dort noch nicht existiert (um Original-Werte nicht zu überschreiben)
                                if (!replacements.ContainsKey(newVal))
                                {
                                    replacements[newVal] = oldVal;
                                }
                            }
                        }
                    }

                    // 2) root.Options
                    if (root.Options != null)
                    {
                        foreach (var opt in root.Options)
                        {
                            if (opt.Files == null) continue;
                            var keys = opt.Files.Keys.ToList();
                            foreach (var key in keys)
                            {
                                var oldVal = opt.Files[key];
                                if (!string.IsNullOrEmpty(oldVal) && paps.ContainsKey(oldVal))
                                {
                                    var newVal = paps[oldVal];
                                    opt.Files[key] = newVal;
                                    modified = true;

                                    // ins replacements-Dictionary eintragen
                                    if (!replacements.ContainsKey(newVal))
                                    {
                                        replacements[newVal] = oldVal;
                                    }
                                }
                            }
                        }
                    }

                    if (modified)
                    {
                        string updated = JsonConvert.SerializeObject(root, Formatting.Indented);
                        File.WriteAllText(file, updated);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Fehler beim Verarbeiten von {file}: {e.Message}", e);
                }
            }

            // Abschließend replacements.json aktualisieren
            File.WriteAllText(replacementsFile, JsonConvert.SerializeObject(replacements, Formatting.Indented));
        }

        /// <summary>
        /// ResetMod:
        /// 1) Löscht Soundy-Group (alle group_*_soundy.json im Hauptverzeichnis).
        /// 2) Prüft, ob soundy/replacements.json existiert. Falls nicht, Abbruch.
        /// 3) Anhand der Einträge in replacements.json werden in allen .json-Dateien
        ///    alle Pfade, die im replacements-Dict als Keys stehen, durch den entsprechenden Old-Pfad (Value) ersetzt.
        /// </summary>
        public static void ResetMod(string dirPath)
        {
            // 1) Soundy group löschen (alle group_*_soundy.json)
            var soundyGroupFiles = Directory.GetFiles(dirPath, "group_*_soundy.json", SearchOption.TopDirectoryOnly);
            foreach (var file in soundyGroupFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ggf. Logging
                }
            }

            // 2) replacements.json suchen
            string soundyDir = Path.Combine(dirPath, "soundy");
            string replacementsPath = Path.Combine(soundyDir, "replacements.json");
            if (!File.Exists(replacementsPath))
            {
                Directory.Delete(soundyDir, true);
                return; // Nichts zu tun
            }

            // 3) Dictionary laden und Rückgängigmachen in allen .json-Dateien
            Dictionary<string, string>? replacements = null;
            try
            {
                var content = File.ReadAllText(replacementsPath);
                replacements = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
            }
            catch
            {
                // Fehler beim Laden -> kein Rückgängig
            }

            if (replacements == null || replacements.Count == 0)
                return;

            // Wir gehen durch alle Dateien im Hauptverzeichnis
            var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in jsonFiles)
            {
                // Falls es die replacements.json selbst ist, überspringen
                if (file.EndsWith("replacements.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    string json = File.ReadAllText(file);
                    var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
                    if (root == null)
                        continue;

                    bool modified = false;

                    // 1) root.Files rückgängig machen
                    if (root.Files != null)
                    {
                        var keys = root.Files.Keys.ToList();
                        foreach (var key in keys)
                        {
                            var currentVal = root.Files[key];
                            if (!string.IsNullOrEmpty(currentVal) && replacements.ContainsKey(currentVal))
                            {
                                // zurücksetzen
                                root.Files[key] = replacements[currentVal];
                                modified = true;
                            }
                        }
                    }

                    // 2) root.Options rückgängig machen
                    if (root.Options != null)
                    {
                        foreach (var opt in root.Options)
                        {
                            if (opt.Files == null) continue;
                            var keys = opt.Files.Keys.ToList();
                            foreach (var key in keys)
                            {
                                var currentVal = opt.Files[key];
                                if (!string.IsNullOrEmpty(currentVal) && replacements.ContainsKey(currentVal))
                                {
                                    opt.Files[key] = replacements[currentVal];
                                    modified = true;
                                }
                            }
                        }
                    }

                    if (modified)
                    {
                        string updated = JsonConvert.SerializeObject(root, Formatting.Indented);
                        File.WriteAllText(file, updated);
                    }
                }
                catch
                {
                    // ggf. Logging
                }
            }

            Directory.Delete(soundyDir, true);
        }
    }

    internal class PenumbraJson
    {
        [JsonProperty("ModDirectory")]
        public string? ModDirectory { get; set; }
    }

    // Hilfsklassen für die JSON-Struktur:
    public class SoundyJsonRoot
    {
        public string? Version { get; set; }
        public string? Name { get; set; } = "";

        public Dictionary<string, string>? Files = new Dictionary<string, string>();
        public string? Description { get; set; }
        public int? Priority { get; set; }
        public string? Type { get; set; } = "";
        public int? DefaultSettings { get; set; }
        public List<Option>? Options { get; set; } = new List<Option>();
    }

    public class Option
    {
        public string? Name { get; set; } = "";
        public string? Description { get; set; }
        public Dictionary<string, string>? Files { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string>? FileSwaps { get; set; } = new Dictionary<string, string>();
        public List<object>? Manipulations { get; set; } = new List<object>();
    }

    public class GroupedScdEntry
    {
        public string ScdPath { get; set; } = "";
        public List<ScdReference> References { get; set; } = new List<ScdReference>();
        public bool Selected { get; set; } = false; // Für das UI: Überschreiben?
    }

    public class ScdReference
    {
        public string JsonFile { get; set; } = "";
        public string OptionName { get; set; } = "";
    }

}
