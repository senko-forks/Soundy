using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ImGuiNET;
using YTImport;

namespace YTImport.Windows
{
    public class DeleteWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        // Dropdown selections
        private string? selectedJsonFile = null;
        private string? selectedEntry = null;

        // List of JSON files and entries
        private string[] jsonFiles = Array.Empty<string>();
        private string[] jsonEntries = Array.Empty<string>();

        // Flags to track if data needs to be refreshed
        private bool refreshJsonFiles = true;
        private bool refreshJsonEntries = true;

        // Status messages
        private string processState = "";

        public DeleteWindow(Plugin plugin)
            : base(
                  "Delete Entry",
                  ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize)
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 200),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text("Select a JSON file:");
            if (refreshJsonFiles)
            {
                LoadJsonFiles();
                refreshJsonFiles = false;
            }

            if (ImGui.BeginCombo("##JsonFiles", selectedJsonFile ?? "Select a JSON File"))
            {
                foreach (var file in jsonFiles)
                {
                    bool isSelected = (file == selectedJsonFile);
                    if (ImGui.Selectable(file, isSelected))
                    {
                        selectedJsonFile = file;
                        selectedEntry = null;
                        refreshJsonEntries = true;
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            if (selectedJsonFile != null)
            {
                ImGui.Spacing();
                ImGui.Text("Select an Entry:");
                if (refreshJsonEntries)
                {
                    LoadJsonEntries(selectedJsonFile);
                    refreshJsonEntries = false;
                }

                if (jsonEntries.Length > 0)
                {
                    if (ImGui.BeginCombo("##JsonEntries", selectedEntry ?? "Select an Entry"))
                    {
                        foreach (var entry in jsonEntries)
                        {
                            bool isSelected = (entry == selectedEntry);
                            if (ImGui.Selectable(entry, isSelected))
                            {
                                selectedEntry = entry;
                            }
                            if (isSelected)
                                ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "No entries available for deletion.");
                }
            }

            ImGui.Spacing();

            // Delete Button
            if (!string.IsNullOrEmpty(selectedJsonFile) && !string.IsNullOrEmpty(selectedEntry))
            {
                if (ImGui.Button("Delete Entry"))
                {
                    DeleteSelectedEntry();
                    plugin.RefreshMods();
                }
            }

            ImGui.Separator();

            // Status Message
            if (!string.IsNullOrEmpty(processState))
            {
                ImGui.TextWrapped(processState);
            }
        }

        /// <summary>
        /// Loads all relevant JSON files from the DJPath.
        /// </summary>
        private void LoadJsonFiles()
        {
            try
            {
                var djPath = plugin.Configuration.DJPath;
                if (string.IsNullOrWhiteSpace(djPath) || !Directory.Exists(djPath))
                {
                    jsonFiles = Array.Empty<string>();
                    return;
                }

                // Assuming JSON files follow the pattern "group_*_*.json"
                jsonFiles = Directory.GetFiles(djPath, "group_*_*.json", SearchOption.TopDirectoryOnly)
                                    .Select(Path.GetFileName)
                                    .ToArray();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error loading JSON files: {ex.Message}");
                jsonFiles = Array.Empty<string>();
            }
        }

        /// <summary>
        /// Loads all entries from the selected JSON file, excluding "Omit".
        /// </summary>
        /// <param name="jsonFileName">The selected JSON file name.</param>
        private void LoadJsonEntries(string jsonFileName)
        {
            try
            {
                var djPath = plugin.Configuration.DJPath;
                var jsonPath = Path.Combine(djPath, jsonFileName);
                if (!File.Exists(jsonPath))
                {
                    jsonEntries = Array.Empty<string>();
                    return;
                }

                var djData = DJImporter.LoadDJJson(plugin, jsonPath);
                if (djData == null)
                {
                    jsonEntries = Array.Empty<string>();
                    return;
                }

                // Exclude "Omit" entry
                jsonEntries = djData.Options
                                    .Where(option => !string.Equals(option.Name, "Omit", StringComparison.OrdinalIgnoreCase))
                                    .Select(option => option.Name)
                                    .ToArray();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error loading JSON entries: {ex.Message}");
                jsonEntries = Array.Empty<string>();
            }
        }

        /// <summary>
        /// Deletes the selected entry and its associated file.
        /// If all entries except "Omit" are deleted, deletes the JSON file and its directory.
        /// </summary>
        private void DeleteSelectedEntry()
        {
            if (string.IsNullOrEmpty(selectedJsonFile) || string.IsNullOrEmpty(selectedEntry))
            {
                processState = "Please select a JSON file and an entry to delete.";
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    var djPath = plugin.Configuration.DJPath;
                    var jsonPath = Path.Combine(djPath, selectedJsonFile);
                    var djData = DJImporter.LoadDJJson(plugin, jsonPath);

                    if (djData == null)
                    {
                        processState = "Failed to load the selected JSON file.";
                        Plugin.Log.Error($"Failed to load JSON file: {jsonPath}");
                        return;
                    }

                    var optionToDelete = djData.Options.FirstOrDefault(opt => opt.Name == selectedEntry);
                    if (optionToDelete == null)
                    {
                        processState = "Selected entry not found in the JSON file.";
                        return;
                    }

                    // Delete associated files
                    foreach (var file in optionToDelete.Files.Values)
                    {
                        var filePath = Path.Combine(djPath, file.Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            Plugin.Log.Information($"Deleted file: {filePath}");
                        }
                        else
                        {
                            Plugin.Log.Warning($"File not found for deletion: {filePath}");
                        }
                    }

                    // Remove the option from the JSON
                    djData.Options.Remove(optionToDelete);

                    // Save the updated JSON
                    DJImporter.SaveDJJson(plugin, jsonPath, djData);
                    Plugin.Log.Information($"Deleted entry '{selectedEntry}' from {selectedJsonFile}");

                    // Check if only "Omit" remains
                    var remainingOptions = djData.Options
                                                .Where(opt => !string.Equals(opt.Name, "Omit", StringComparison.OrdinalIgnoreCase))
                                                .ToList();

                    if (remainingOptions.Count == 0)
                    {
                        // Delete the JSON file
                        File.Delete(jsonPath);
                        Plugin.Log.Information($"Deleted JSON file as no entries remain: {jsonPath}");

                        // Optionally, delete the directory if it's empty
                        var customDir = Path.GetDirectoryName(jsonPath);
                        if (customDir != null && Directory.Exists(customDir) && !Directory.EnumerateFileSystemEntries(customDir).Any())
                        {
                            Directory.Delete(customDir);
                            Plugin.Log.Information($"Deleted empty directory: {customDir}");
                        }

                        // Refresh the JSON files list
                        Svc.Framework.RunOnTick(() =>
                        {
                            refreshJsonFiles = true;
                            selectedJsonFile = null;
                            selectedEntry = null;
                            refreshJsonEntries = true;
                        });

                        processState = $"Deleted entry '{selectedEntry}' and removed JSON file as no entries remain.";
                        return;
                    }

                    // Refresh the entries list
                    Svc.Framework.RunOnTick(() =>
                    {
                        refreshJsonEntries = true;
                        selectedEntry = null;
                    });

                    processState = $"Deleted entry '{selectedEntry}' successfully.";
                }
                catch (Exception ex)
                {
                    processState = $"Error deleting entry: {ex.Message}";
                    Plugin.Log.Error($"Error deleting entry: {ex.Message}");
                }
            });
        }
    }
}
