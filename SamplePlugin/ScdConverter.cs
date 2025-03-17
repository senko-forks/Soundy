using System;
using System.IO;
using System.Collections.Generic;

namespace BuildScd
{
    public static class Builder
    {
        // Definiere den festen Offset für den Sound Entry Header
        private const int SoundEntryHeaderOffset = 0x1E0;

        public static void BuildNewScd(
            string templateScdPath,
            string oldOggPath,    // X
            string newOggPath,    // Z
            string outputScdPath)
        {
            // Lesen der Dateien
            byte[] scdBytes = File.ReadAllBytes(templateScdPath);
            byte[] oldOggBytes = File.ReadAllBytes(oldOggPath);
            byte[] newOggBytes = File.ReadAllBytes(newOggPath);

            // Umwandeln in eine List<byte> zur einfacheren Manipulation
            List<byte> scdList = new List<byte>(scdBytes);

            // Suchen der alten OGG-Daten in der SCD-Datei
            int oldOggStartPos = FindSubArray(scdBytes, oldOggBytes);
            if (oldOggStartPos == -1)
            {
                Console.WriteLine("Die alte OGG-Datei wurde in der SCD-Datei nicht gefunden.");
                return;
            }

            Console.WriteLine($"Alte OGG-Datei gefunden bei Position: 0x{oldOggStartPos:X}");

            // Entfernen der alten OGG-Daten
            scdList.RemoveRange(oldOggStartPos, oldOggBytes.Length);
            Console.WriteLine($"Alte OGG-Daten entfernt.");

            // Berechnung der neuen Gesamtdateigröße
            int newTotalFileSize = scdList.Count + newOggBytes.Length;

            // Aktualisieren des Dateigrößenfeldes bei Offset 0x10
            WriteInt32LittleEndian(scdList, 0x10, newTotalFileSize);
            Console.WriteLine($"Dateigröße bei Offset 0x10 auf {newTotalFileSize} gesetzt.");

            // Berechnung der neuen Position für die OGG-Daten (altes Startpos)
            int newOggPos = oldOggStartPos;

            // Schreiben der neuen OGG-Daten an der alten Position
            scdList.InsertRange(newOggPos, newOggBytes);
            Console.WriteLine($"Neue OGG-Daten an Position 0x{newOggPos:X} eingefügt.");


            // Berechnung des LoopEnd-Werts
            int loopEnd = newOggBytes.Length >= 4000 ? newOggBytes.Length - 4000 : newOggBytes.Length;
            Console.WriteLine($"LoopEnd gesetzt auf: {loopEnd}");

            // Setze die spezifischen Felder im Sound Entry Header
            UpdateSoundEntryHeader(scdList, newOggBytes.Length, loopEnd, newOggPos);

            // Schreiben der modifizierten SCD-Datei
            File.WriteAllBytes(outputScdPath, scdList.ToArray());
            Console.WriteLine($"SCD-Datei erfolgreich erstellt: {outputScdPath}");
        }

        /// <summary>
        /// Sucht eine Subarray (needle) innerhalb eines Arrays (haystack) und gibt den Startindex zurück.
        /// Gibt -1 zurück, wenn die Subarray nicht gefunden wird.
        /// </summary>
        private static int FindSubArray(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0) return -1;
            if (needle.Length > haystack.Length) return -1;

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }

        /// <summary>
        /// Aktualisiert spezifische Felder im Sound Entry Header.
        /// </summary>
        private static void UpdateSoundEntryHeader(List<byte> scdList, int newOggLength, int loopEnd, int newOggPos)
        {
            // Musik Dateilänge (0x00)
            WriteInt32LittleEndian(scdList, SoundEntryHeaderOffset + 0x00, newOggLength);
            Console.WriteLine($"MusicFileLength (0x00) auf {newOggLength} gesetzt.");

            // Loop End (0x14) - auskommentiert für Tests oder gesetzt auf neuen Wert
            WriteInt32LittleEndian(scdList, SoundEntryHeaderOffset + 0x14, loopEnd);
            Console.WriteLine($"LoopEnd (0x14) auf {loopEnd} gesetzt.");
        }

        /// <summary>
        /// Schreibt einen Int32-Wert in Little Endian an eine spezifische Position in der List<byte>.
        /// </summary>
        private static void WriteInt32LittleEndian(List<byte> scdList, int position, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            // Stelle sicher, dass die Liste groß genug ist
            while (scdList.Count < position + 4)
                scdList.Add(0);

            for (int i = 0; i < 4; i++)
            {
                scdList[position + i] = bytes[i];
            }
        }

        /// <summary>
        /// Schreibt einen Int16-Wert in Little Endian an eine spezifische Position in der List<byte>.
        /// </summary>
        private static void WriteInt16LittleEndian(List<byte> scdList, int position, short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            // Stelle sicher, dass die Liste groß genug ist
            while (scdList.Count < position + 2)
                scdList.Add(0);

            for (int i = 0; i < 2; i++)
            {
                scdList[position + i] = bytes[i];
            }
        }
    }
}
