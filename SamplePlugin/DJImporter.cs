using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace YTImport
{
    public static class DJImporter
    {
        /// <summary>
        /// Import-Methode:
        /// 1) Searches all JSON files in the format "group_*_*.json" in the DJ directory.
        /// 2) If no "custom" file is found, creates a new "group_{next}_custom.json".
        ///    Where {next} = Number of existing group_*_*.json files + 1, starting at 1.
        /// 3) Loads the target JSON file, adds a new entry, and saves it.
        /// </summary>
        /// <param name="plugin">The Plugin object (for configuration and logging)</param>
        /// <param name="name">Name of the new options entry</param>
        /// <param name="filename">Filename to be linked under "custom\..."</param>
        public static void Import(Plugin plugin, string name, string filename, string playlistName)
        {
            var dir = plugin.Configuration.DJPath;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Plugin.Log.Error($"DJ Path invalid: {dir}");
                return;
            }

            // 1) Search all files matching "group_*_*.json"
            var allGroupFiles = Directory.GetFiles(dir, "group_*_*.json", SearchOption.TopDirectoryOnly);

            // 2) Check if a "custom" file exists
            var customFiles = allGroupFiles.Where(f => f.Contains(playlistName, StringComparison.OrdinalIgnoreCase)).ToArray();

            string targetFile;
            if (customFiles.Length == 0)
            {
                // No "custom" file found, create a new one
                int nextNumber = allGroupFiles.Length + 1; // Starts at 1
                var newFileName = $"group_{nextNumber:D2}_{playlistName}.json"; // e.g., group_01_custom.json
                targetFile = Path.Combine(dir, newFileName);

                // Create a new JSON structure (ANHANG1)
                var newData = CreateDefaultCustomJson(playlistName);
                SaveDJJson(plugin, targetFile, newData);

                Plugin.Log.Information($"Created new custom group file: {targetFile}");
            }
            else
            {
                // "custom" file found, use the first one
                targetFile = customFiles[0];
                Plugin.Log.Information($"Using existing custom group file: {targetFile}");
            }

            // 3) Load the existing (or newly created) JSON
            var djData = LoadDJJson(plugin, targetFile);
            if (djData == null)
            {
                Plugin.Log.Error($"Failed to load or parse {targetFile}");
                return;
            }

            // 4) Create a new options entry (ANHANG2)
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

            // 5) Add the new entry without removing old entries
            djData.Options.Add(newOption);

            // 6) Save the updated JSON back
            SaveDJJson(plugin, targetFile, djData);

            Plugin.Log.Information($"Added new custom option \"{name}\" to {targetFile}");
        }

        /// <summary>
        /// Creates the default structure for a new "custom" JSON file (ANHANG1).
        /// </summary>
        /// <returns>A new DJJson object with an "Omit" entry</returns>
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
                    // Keep "Omit" as the first entry
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
        /// Loads a JSON file into a DJJson object.
        /// </summary>
        /// <param name="plugin">The Plugin object (for logging)</param>
        /// <param name="path">The path to the JSON file</param>
        /// <returns>The loaded DJJson object or null on failure</returns>
        public static DJJson? LoadDJJson(Plugin plugin, string path)
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
        /// Saves the DJJson object to a JSON file.
        /// </summary>
        /// <param name="plugin">The Plugin object (for logging)</param>
        /// <param name="path">The path to the JSON file</param>
        /// <param name="data">The DJJson object to save</param>
        public static void SaveDJJson(Plugin plugin, string path, DJJson data)
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
        /// Defines JSON serializer options.
        /// </summary>
        /// <param name="writeIndent">Whether to format the JSON with indentation</param>
        /// <returns>A JsonSerializerOptions object</returns>
        private static JsonSerializerOptions JsonOptions(bool writeIndent = false)
        {
            return new JsonSerializerOptions
            {
                WriteIndented = writeIndent,
                PropertyNamingPolicy = null, // Use property names directly (PascalCase)
                // Optional: AllowTrailingCommas, ReadCommentHandling etc.
            };
        }
    }

    /// <summary>
    /// Represents the JSON structure of "custom" files.
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
    /// Represents the entries within the "Options" array of the JSON file.
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
