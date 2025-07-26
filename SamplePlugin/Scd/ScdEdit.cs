using System;
using System.IO;
using System.Numerics;
using VfxEditor.ScdFormat;
using VfxEditor.ScdFormat.Music.Data; // Für ScdVorbis, ScdAudioData etc.
using NAudio.Wave;
using ECommons.DalamudServices;
using System.Collections.Generic;

namespace Soundy.Scd
{
    public static class ScdEdit
    {
        /// <summary>
        /// Liest ein Template-SCD (das bereits einen funktionierenden Template-OGG enthält) ein, 
        /// ersetzt in dem Audio-Eintrag den Template-OGG durch den neuen OGG und speichert das Ergebnis unter newScdPath.
        /// </summary>
        /// <param name="scdTemplatePath">Pfad zum Template-SCD (funktionierendes SCD mit template OGG)</param>
        /// <param name="oggTemplatePath">(Optional) Pfad, der als Marker im Template dient – falls benötigt</param>
        /// <param name="newScdPath">Pfad, an dem das neue SCD gespeichert werden soll</param>
        /// <param name="replaceOggPath">Pfad zur neuen OGG-Datei, die den Template ersetzen soll</param>
        /// <param name="enableLoop">Soll das Ergebnis geloopt werden?</param>
        /// <returns>true, wenn die Ersetzung erfolgreich war; false, wenn kein passender Audio-Eintrag gefunden wurde</returns>
        public static void CreateScd(string scdTemplatePath, string newScdPath, string oggPath, bool enableLoop = true)
        {
            using (var fs = File.OpenRead(scdTemplatePath))
            using (var br = new BinaryReader(fs))
            {
                ScdFile file = new ScdFile(br, verify: true);

                Plugin.Log.Information("Template-Audio-Daten vorhanden: " + (file.Audio.Count > 0));
                var original = file.Audio[0];

                ScdAudioEntry newData;
                try
                {
                    newData = ScdVorbis.ImportOgg(oggPath, original);
                    // 1) Gesamtlänge in Sekunden holen
                    WaveStream stream = newData.Data.GetStream();
                    TimeSpan totalTime = stream.TotalTime;
                    float totalSec = (float)totalTime.TotalSeconds;

                    var lstart = newData.Data.TimeToBytes(0f);
                    var lend = newData.Data.TimeToBytes(totalSec);
                    if (!enableLoop)
                    {
                        lend = newData.Data.TimeToBytes(0f);
                    }
                    newData.LoopStart = lstart;
                    newData.LoopEnd = lend;

                    Plugin.Log.Information("ImportOgg zurück: " + (newData != null));
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error("CATCH in ImportOgg: " + ex);
                    throw;
                }

                if (newData == null)
                {
                    Plugin.Log.Error("ImportOgg hat null zurückgegeben.");
                    throw new Exception("Audio-Import fehlgeschlagen.");
                }

                file.Replace(original, newData);

                Plugin.Log.Information("Audio-Daten erfolgreich ersetzt.");


                using (var fsOut = File.Create(newScdPath))
                using (var bw = new BinaryWriter(fsOut))
                {
                    long startPos = fsOut.Position;
                    file.Write(bw);
                    long endPos = fsOut.Position;
                    Plugin.Log.Information($"SCD geschrieben: {endPos - startPos} Bytes");

                }
            }
        }
    }
}
