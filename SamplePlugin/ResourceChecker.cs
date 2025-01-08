using System;
using System.IO;
using Dalamud; // Falls du PluginInterface brauchst
using SamplePlugin;
public static class ResourceChecker
{
    public static void CheckDJ(Plugin plugin)
    {
        var djPath = plugin.Configuration.DJPath;

        // 1) Prüfen, ob djPath angegeben und vorhanden ist.
        //    Falls nicht, pfad zurücksetzen und beenden.
        if (string.IsNullOrWhiteSpace(djPath) || !Directory.Exists(djPath))
        {
            plugin.Configuration.DJPath = "";
            plugin.Configuration.Save();
            return;
        }

        // 2) Wenn djPath existiert, prüfen wir, ob es einen "custom"-Unterordner gibt.
        var customFolder = Path.Combine(djPath, "custom");
        if (!Directory.Exists(customFolder))
        {
            Directory.CreateDirectory(customFolder);
        }
    }

    public static void CheckResources()
    {
        // 1) Ermitteln, wo dein Plugin-Assembly liegt
        var assDir = Path.GetDirectoryName(Plugin.PluginInterface.AssemblyLocation.FullName);

        // 2) Lokale Pfade für "resources" und "tools"
        var localResourcesDir = Path.Combine(assDir, "resources");
        var localToolsDir = Path.Combine(assDir, "tools");

        // 3) Aus der Plugin-Konfiguration lesen wir z.B. so:
        //    (Annahme: du hast z. B. Plugin.Configuration.Resources und 
        //     Plugin.Configuration.Tools als Pfadangaben in deiner Config)
        var configResourcesDir = Path.GetFullPath(Configuration.Resources);
        var configToolsDir = Path.GetFullPath(Configuration.Tools);

        // 4) Sicherstellen, dass die Verzeichnisse zusammenpassen:
        EnsureDirectoryMatches(localResourcesDir, configResourcesDir);
        EnsureDirectoryMatches(localToolsDir, configToolsDir);
    }

    /// <summary>
    /// Kopiert alle Dateien aus sourceDir nach targetDir, wenn:
    /// - targetDir nicht existiert (dann wird es erstellt)
    /// - die Datei in targetDir fehlt
    /// - oder sie sich in der Dateigröße unterscheidet
    /// </summary>
    private static void EnsureDirectoryMatches(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            // Falls dein localResourcesDir oder localToolsDir gar nicht existieren,
            // kannst du entscheiden, ob du eine Exception wirfst oder
            // einfach abbrechen willst.
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Zielverzeichnis anlegen, wenn es nicht existiert
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        // Alle Dateien aus sourceDir durchgehen
        var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly);
        foreach (var srcFilePath in sourceFiles)
        {
            var fileName = Path.GetFileName(srcFilePath);
            var destFilePath = Path.Combine(targetDir, fileName);

            // Überprüfen, ob die Datei fehlt oder sich in Größe unterscheidet
            bool needsCopy = true;
            if (File.Exists(destFilePath))
            {
                long srcSize = new FileInfo(srcFilePath).Length;
                long destSize = new FileInfo(destFilePath).Length;
                if (srcSize == destSize)
                {
                    needsCopy = false;
                }
            }

            // Kopieren, falls nötig
            if (needsCopy)
            {
                File.Copy(srcFilePath, destFilePath, overwrite: true);
            }
        }

        // Optional: Falls du sicherstellen willst, dass keine "zusätzlichen" Dateien 
        // im targetDir liegen, könntest du hier Dateien entfernen, 
        // die nicht im sourceDir existieren. Das wäre z. B.:
        // RemoveExtraFiles(targetDir, sourceDir);
    }

    /// <summary>
    /// Optionales Hilfsbeispiel: Entfernt alle Dateien im targetDir, die 
    /// im sourceDir nicht existieren. (Nur wenn du so ein „Spiegeln“ wünschst.)
    /// </summary>
    private static void RemoveExtraFiles(string targetDir, string sourceDir)
    {
        var targetFiles = Directory.GetFiles(targetDir, "*", SearchOption.TopDirectoryOnly);
        foreach (var targetFilePath in targetFiles)
        {
            var fileName = Path.GetFileName(targetFilePath);
            var correspondingSourceFile = Path.Combine(sourceDir, fileName);
            if (!File.Exists(correspondingSourceFile))
            {
                // Lösche Dateien, die nicht im sourceDir vorhanden sind
                File.Delete(targetFilePath);
            }
        }
    }
}
