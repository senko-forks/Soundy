using System;
using System.IO;

namespace Soundy
{
    public static class TempFileCleaner
    {
        public static int DeleteOldTempFiles()
        {
            string tempDir = Path.GetTempPath();
            string[] patterns = { "*.wav", "*.ogg", "*.hkx", "tools.zip" };

            var deletedFilesCount = 0;

            foreach (var pattern in patterns)
            {
                string[] files = Directory.GetFiles(tempDir, pattern);

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedFilesCount++;
                    }
                    catch
                    {
                        // Handle exceptions silently
                    }
                }
            }

            return deletedFilesCount;
        }
    }
}
