using Newtonsoft.Json;
using Soundy;
using Soundy.FileAnalyzer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static class PathHelpers
{
    /// <summary>Erzeugt aus einem absoluten <paramref name="fullPath"/> einen
    /// Relative-Pfad bezogen auf <paramref name="root"/>.
    /// Trennt anschließend auf „/“, weil Penumbra das so bevorzugt.</summary>
    public static string ToRelative(string root, string fullPath)
    {
        var rel = Path.GetRelativePath(root, fullPath);
        // Bei bereits relativen Pfaden ändert Path.GetRelativePath nichts
        return rel.Replace('\\', '/');
    }

    /// <summary>
    /// Schneidet alles vor „soundy/“ oder „soundy\“ ab.
    /// Behält den Slash-Stil von Penumbra ("/").
    /// </summary>
    public static string TrimToSoundy(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var idx = path.IndexOf("soundy/", StringComparison.OrdinalIgnoreCase);
        if (idx <= 0)                           // nichts zu trimmen (idx==0 → schon relativ)
            return path.Replace('\\', '/');

        return path.Substring(idx).Replace('\\', '/');
    }
    public static void FixSoundyJson(string jsonFile)
    {
        var json = File.ReadAllText(jsonFile);
        var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
        if (root is null) return;

        bool changed = false;

        void FixDict(IDictionary<string, string>? dict)
        {
            if (dict is null) return;
            foreach (var key in dict.Keys.ToList())
            {
                var oldVal = dict[key];
                var newVal = TrimToSoundy(oldVal);
                if (newVal != oldVal)
                {
                    dict[key] = newVal;
                    changed = true;
                }
            }
        }

        FixDict(root.Files);
        if (root.Options != null)
            foreach (var opt in root.Options) FixDict(opt.Files);

        if (changed)
            File.WriteAllText(jsonFile,
                JsonConvert.SerializeObject(root, Formatting.Indented));
    }
    public static async Task RunOneShotFixAsync(string penumbraPath)
    {
        // alle *_soundy.json unterhalb von Penumbra (inkl. Unterordner = Mods)
        var soundyFiles = Directory.GetFiles(penumbraPath, "*_soundy.json",
                                             SearchOption.AllDirectories);

        // IO + JSON-Parsing in den Thread-Pool auslagern
        foreach (var file in soundyFiles)
            await Task.Run(() => FixSoundyJson(file));
    }


}
