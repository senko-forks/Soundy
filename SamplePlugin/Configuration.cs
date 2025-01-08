using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.IO;

namespace SamplePlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // Beispiel: Flag aus dem Sample
        public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

        // Hier hinterlegt der User den Pfad, wohin wir speichern wollen.
        public static string BasePath = Plugin.PluginInterface.GetPluginConfigDirectory();
        public static string Tools = Path.Combine(BasePath, "tools");
        public static string Resources = Path.Combine(BasePath, "resources");

        public string DJPath { get; set; } = "";

        public void Save()
        {
            // Standard: Speichert unsere Config in die Plugin-Konfiguration
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
