using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using VfxEditor.PapFormat;
using VfxEditor.Parsing;
using VfxEditor.TmbFormat;
using VfxEditor.TmbFormat.Entries; // für den C063-Typ

namespace Soundy.Pap
{
    public static class PapInjector
    {
        /// <summary>
        /// Liest die gesamte Pap-Datei in einen Byte-Array, modifiziert den Havok-Datenbereich (falls vorhanden) 
        /// so, dass er höchstens 8 Bytes lang ist, und erzeugt dann ein PapFile aus diesen modifizierten Daten.
        /// </summary>
        public static PapFile LoadPap(string path)
        {
            var random = new Random();
            int rand = random.Next(9999);
            int rand2 = random.Next(9999);

            try
            {
                string hkxTemp = Path.Combine(Path.GetTempPath(), $"{rand}_{rand2}" + ".hkx");
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    // init = true: Havok-Daten werden normal gelesen
                    return new PapFile(br, path, hkxTemp, true, true);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        /// <summary>
        /// Hilfsmethode, um per Reflection den Value einer ParsedInt-Instanz zu setzen.
        /// </summary>
        private static void SetParsedIntValue(object instance, string fieldName, int newValue)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new Exception($"Feld '{fieldName}' nicht gefunden in {instance.GetType().Name}.");

            var parsedInt = field.GetValue(instance);
            if (parsedInt == null)
                throw new Exception($"Feld '{fieldName}' in {instance.GetType().Name} ist null.");

            // Zuerst nach einer öffentlichen Property "Value" suchen
            var prop = parsedInt.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(parsedInt, newValue);
                return;
            }

            // Falls die Property nicht existiert, versuche ein öffentliches Feld "Value"
            var publicField = parsedInt.GetType().GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            if (publicField != null)
            {
                publicField.SetValue(parsedInt, newValue);
                return;
            }

            throw new Exception($"Keine Eigenschaft oder Feld 'Value' gefunden in {parsedInt.GetType().Name} (Feld '{fieldName}').");
        }


        /// <summary>
        /// Hilfsmethode, um per Reflection den Value einer TmbOffsetString-Instanz zu setzen.
        /// </summary>
        private static void SetTmbOffsetStringValue(object instance, string fieldName, string newValue)
        {
            // Hole das private Feld (z. B. "Path") aus der Instanz (in C063)
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new Exception($"Feld '{fieldName}' nicht gefunden in {instance.GetType().Name}.");

            var tmbOffsetString = field.GetValue(instance);
            if (tmbOffsetString == null)
                throw new Exception($"Feld '{fieldName}' in {instance.GetType().Name} ist null.");

            // Versuche zuerst, eine öffentliche Property "Value" zu bekommen.
            var prop = tmbOffsetString.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(tmbOffsetString, newValue);
                return;
            }

            // Falls nicht vorhanden, prüfe, ob es ein öffentliches Feld "Value" gibt.
            var fieldValue = tmbOffsetString.GetType().GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            if (fieldValue != null)
            {
                fieldValue.SetValue(tmbOffsetString, newValue);
                return;
            }

            // Falls auch das nicht klappt, versuche die Property "Text"
            prop = tmbOffsetString.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(tmbOffsetString, newValue);
                return;
            }

            throw new Exception($"Keine Eigenschaft oder Feld 'Value' oder 'Text' gefunden in {tmbOffsetString.GetType().Name} (Feld '{fieldName}').");
        }

        public static string GetSCDPathFromC063(C063 entry)
        {
            // Hier per Reflection (ähnlich wie bei SetTmbOffsetStringValue) den Wert auslesen.
            // Das konkrete Vorgehen hängt von der internen Struktur des C063-Typs ab.
            // Beispiel (sehr vereinfacht):

            var fieldName = "Path";
            var instance = entry;

            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new Exception($"Feld 'Path' nicht gefunden in {instance.GetType().Name}.");

            var tmbOffsetString = field.GetValue(entry);
            if (tmbOffsetString == null)
                throw new Exception($"Feld '{fieldName}' in {instance.GetType().Name} ist null.");

            // Versuche zuerst, eine öffentliche Property "Value" zu bekommen.
            var prop = tmbOffsetString.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                return prop.GetValue(tmbOffsetString)?.ToString() ?? "not found 2";
            }

            // Falls nicht vorhanden, prüfe, ob es ein öffentliches Feld "Value" gibt.
            var fieldValue = tmbOffsetString.GetType().GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            if (fieldValue != null)
            {
                return fieldValue.GetValue(tmbOffsetString)?.ToString() ?? "not found 2";
            }

            // Falls auch das nicht klappt, versuche die Property "Text"
            prop = tmbOffsetString.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                return prop.GetValue(tmbOffsetString)?.ToString() ?? "not found 2";
            }

            return "not found 1";
        }

        /// <summary>
        /// Liest die alte Pap-Datei, fügt (bzw. aktualisiert) in jeder Animation den C063-Sound-Eintrag
        /// und schreibt die modifizierte Datei unter dem neuen Pfad ab.
        /// </summary>
        /// <param name="scdPathInGame">Interner Soundpfad, z. B. "sound/something/loop.scd".</param>
        /// <param name="oldPapPath">Pfad der Original-.pap-Datei.</param>
        /// <param name="newPapPath">Pfad, an dem die modifizierte .pap abgelegt wird.</param>
        public static void InjectSound(string scdPathInGame, string oldPapPath, string newPapPath)
        {
            PapFile pap = null;
            try
            {
                pap = LoadPap(oldPapPath);
                Plugin.Log.Error($"Pap erfolgreich geladen: {oldPapPath}");
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Fehler beim Laden der Pap: {oldPapPath}. Exception: {e}");
                throw new Exception($"Fehler beim Laden der Pap: {e}");
            }

            try
            {
                // Durchlaufe alle Animationen
                foreach (var anim in pap.Animations)
                {
                    try
                    {
                        if (anim.Tmb == null)
                        {
                            Plugin.Log.Error("Animation übersprungen: Tmb ist null");
                            continue;
                        }
                        if (anim.Tmb.Actors == null || anim.Tmb.Actors.Count == 0)
                        {
                            Plugin.Log.Error("Animation übersprungen: Keine Actors vorhanden");
                            continue;
                        }

                        var actor = anim.Tmb.Actors[0]; // Beispiel: erster Actor
                        Plugin.Log.Error("Verarbeite Actor in Animation");

                        Tmtr soundTrack = null;
                        // Hier könntest du (falls gewünscht) nach einem vorhandenen Track suchen.
                        // Momentan ist die Suche auskommentiert – wir gehen direkt zur Erstellung eines neuen Tracks über.

                        if (soundTrack == null)
                        {
                            try
                            {
                                soundTrack = new Tmtr(anim.Tmb);
                                soundTrack.Time.Value = 0;
                                actor.Tracks.Add(soundTrack);
                                anim.Tmb.AllTracks.Add(soundTrack);
                                Plugin.Log.Error("Neuer Sound-Track erstellt und hinzugefügt");
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.Error($"Fehler beim Erzeugen/Hinzufügen des Sound-Tracks: {ex}");
                                throw;
                            }
                        }

                        try
                        {
                            var soundEntry = soundTrack.Entries.OfType<C063>().FirstOrDefault();
                            if (soundEntry == null)
                            {
                                soundEntry = new C063(anim.Tmb);
                                soundEntry.Time.Value = 1;  // Relative Zeit im Track
                                soundTrack.Entries.Add(soundEntry);
                                anim.Tmb.AllEntries.Add(soundEntry);
                                Plugin.Log.Error("Neuer Sound-Entry erstellt und hinzugefügt");
                            }

                            // Setze die Parameter des C063-Eintrags
                            SetParsedIntValue(soundEntry, "Loop", -1);
                            SetParsedIntValue(soundEntry, "Interrupt", 0);
                            SetTmbOffsetStringValue(soundEntry, "Path", scdPathInGame);
                            SetParsedIntValue(soundEntry, "SoundIndex", 0);
                            SetParsedIntValue(soundEntry, "SoundPosition", 99);
                            Plugin.Log.Error("Sound-Entry Parameter gesetzt");
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.Error($"Fehler beim Verarbeiten des Sound-Entries in Animation: {ex}");
                            throw;
                        }
                    }
                    catch (Exception innerEx)
                    {
                        Plugin.Log.Error($"Fehler in einer Animation: {innerEx}");
                        // Hier kannst du entscheiden, ob du fortsetzen oder den Fehler erneut werfen möchtest:
                        // continue;
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Fehler beim Durchlaufen der Animationen: {ex}");
                throw;
            }

            try
            {
                using (var fsOut = File.Create(newPapPath))
                using (var bw = new BinaryWriter(fsOut))
                {
                    pap.Write(bw);
                    Plugin.Log.Error($"Pap erfolgreich geschrieben: {newPapPath}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Fehler beim Schreiben der Pap nach {newPapPath}: {ex}");
                throw;
            }
        }
    }

}
