using ECommons;
using Newtonsoft.Json;
using Soundy.FileAnalyzer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VfxEditor.PapFormat; // Angenommen, hier liegt die PapFile-Klasse
using VfxEditor.TmbFormat.Entries; // Für den C063-Typ
using ECommons.DalamudServices;

namespace Soundy.Pap
{
    public static class PapManager
    {
        /// <summary>
        /// Sucht im angegebenen Verzeichnis (und optional in Unterordnern) nach PAP-Dateien,
        /// lädt diese und extrahiert die zugewiesenen SCDs aus den Animationen.
        /// </summary>
        /// <param name="modDirPath">Pfad zum Mod-Ordner (z. B. Penumbra Mod Pfad)</param>
        /// <returns>Liste gruppierter PAP-Einträge mit den extrahierten SCD-Details.</returns>
        public class GroupedPapEntry
        {
            /// <summary>
            /// Enthält den in den JSONs gefundenen PAP-Pfad.
            /// </summary>
            public string PapPath { get; set; } = "";

            /// <summary>
            /// Alle Referenzen aus den JSONs (Dateiname + OptionName), die auf diesen PAP verweisen.
            /// </summary>
            public List<PapReference> References { get; set; } = new List<PapReference>();

            /// <summary>
            /// Hier werden zusätzlich die in der PAP-Datei gefundenen SCD-Einträge abgelegt.
            /// </summary>
            public List<PapScdDetail> ScdDetails { get; set; } = new List<PapScdDetail>();

            /// <summary>
            /// Für das UI, ob dieser Eintrag selektiert wurde.
            /// </summary>
            public bool Selected { get; set; } = false;
        }

        public class PapReference
        {
            public string JsonFile { get; set; } = "";
            public string OptionName { get; set; } = "";
            public string GroupName { get; set; } = "";
        }

        public class PapScdDetail
        {
            /// <summary>
            /// Der in der PAP gefunden SCD-Pfad (kann leer sein, wenn nicht vorhanden).
            /// </summary>
            public string SCDPath { get; set; } = "";

            /// <summary>
            /// Name der zugehörigen Animation.
            /// </summary>
            public string AnimationName { get; set; } = "";

            /// <summary>
            /// Name des Actors (falls relevant).
            /// </summary>
            public string ActorName { get; set; } = "";
        }

        public unsafe static List<GroupedPapEntry> ScanForPapDetailsGrouped(string dirPath, Action<string>? stateUpdate = null)
        {
            // Zuerst: Durchsuche alle JSON-Dateien im Verzeichnis und sammle die PAP-Referenzen.
            var rawList = new List<(string JsonFile, string OptionName, string GroupName, string PapPath)>();

            var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.TopDirectoryOnly);
            var count = 0;
            var countMax = jsonFiles.Length;
            foreach (var file in jsonFiles)
            {
                count++;
                stateUpdate?.Invoke($"Scanning files... ({count}/{countMax})");
                try
                {
                    string json = File.ReadAllText(file);
                    var root = JsonConvert.DeserializeObject<SoundyJsonRoot>(json);
                    if (root == null) continue;

                    // 1) Suche im root.Files
                    if (root.Files != null)
                    {
                        foreach (var key in root.Files.Keys)
                        {
                            if (key.EndsWith(".pap", StringComparison.OrdinalIgnoreCase))
                            {
                                rawList.Add((file, "(root)", root.Name ?? "", root.Files[key]));
                            }
                        }
                    }

                    // 2) Suche in den Options
                    if (root.Options != null)
                    {
                        foreach (var opt in root.Options)
                        {
                            if (opt.Files == null) continue;
                            foreach (var key in opt.Files.Keys)
                            {
                                if (key.EndsWith(".pap", StringComparison.OrdinalIgnoreCase))
                                {
                                    rawList.Add((file, opt.Name ?? "(no name)", root.Name ?? "", opt.Files[key]));
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignorieren, wenn eine JSON "kaputt" ist.
                }
            }

            // Gruppieren der Einträge nach PAP-Pfad:
            var groups = rawList
                .GroupBy(x => x.PapPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GroupedPapEntry
                {
                    PapPath = g.Key,
                    References = g.Select(x => new PapReference
                    {
                        JsonFile = x.JsonFile,
                        OptionName = x.OptionName,
                        GroupName = x.GroupName
                    }).ToList(),
                    ScdDetails = new List<PapScdDetail>()
                })
                .Where(g => File.Exists(Path.Combine(dirPath, g.PapPath)))
                .ToList();

            count = 0;
            countMax = groups.Count;

            // Nun: Für jede gefundene PAP-Datei den Inhalt laden und darin nach SCD-Einträgen suchen.
            foreach (var group in groups)
            {
                count++;
                stateUpdate?.Invoke($"Scanning animations... ({count}/{countMax})");
                // Falls der PAP-Pfad relativ ist, kombinieren wir ihn mit dem Mod-Verzeichnis:
                string absolutePapPath = Path.Combine(dirPath, group.PapPath);
                var random = new Random();
                if (!File.Exists(absolutePapPath))
                    continue;

                try
                {
                    // Hier nehmen wir an, dass du in deinem PapInjector eine Methode implementierst,
                    // die öffentlich zugänglich ist, z. B. LoadPapForScanning, die einen PapFile zurückgibt.
                    PapFile pap = LoadPapOnMainThread(absolutePapPath);

                    // Durchlaufe alle Animationen im PAP.
                    foreach (var anim in pap.Animations)
                    {
                        // Falls Animationen keinen Namen haben, kannst du hier einen Standardwert vergeben.
                        string animationName = string.IsNullOrEmpty(anim.GetName()) ? "Unnamed Animation" : anim.GetName();


                        if (anim.Tmb == null)
                            continue;

                        // Für jeden Actor in der Animation:
                        foreach (var actor in anim.Tmb.Actors)
                        {
                            // Gehe durch alle Tracks und suche nach C063-Einträgen (Sound-Einträgen)
                            foreach (var track in actor.Tracks)
                            {
                                var soundEntries = track.Entries.OfType<C063>().ToList();
                                foreach (var soundEntry in soundEntries)
                                {
                                    // Wir nehmen an, dass du bereits eine Hilfsmethode GetSCDPathFromC063 implementiert hast,
                                    // die per Reflection den aktuell zugewiesenen SCD-Pfad aus einem C063-Eintrag ausliest.
                                    string scdPath = PapInjector.GetSCDPathFromC063(soundEntry);
                                    group.ScdDetails.Add(new PapScdDetail
                                    {
                                        SCDPath = scdPath,
                                        AnimationName = animationName,
                                        ActorName = ""
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    stateUpdate?.Invoke($"{ex}");
                }
            }
            stateUpdate?.Invoke($"");
            return groups;
        }

        public static Task<List<GroupedPapEntry>> ScanForPapDetailsGroupedAsync(string dirPath, Action<string>? stateUpdate = null)
        {
            return Task.Run(() => ScanForPapDetailsGrouped(dirPath, stateUpdate));
        }

        private static PapFile LoadPapOnMainThread(string path)
        {
            var tcs = new TaskCompletionSource<PapFile>();
            Svc.Framework?.RunOnFrameworkThread(() =>
            {
                try
                {
                    tcs.SetResult(PapInjector.LoadPap(path));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task.GetAwaiter().GetResult();
        }

    }
}
