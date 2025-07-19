using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;

namespace Soundy
{
    public static class ToolLoader
    {
        private static bool toolsInitialized = false;

        public static string YtdlpPath { get; private set; } = "";
        public static string FfmpegPath { get; private set; } = "";

        /// <summary>
        /// Initializes tool paths and downloads tools if they are not present.
        /// </summary>
        /// <param name="reportProgress">Callback to report download progress.</param>
        public static async Task InitializeToolsAsync(Action<string> reportProgress, Plugin plugin)
        {
            if (toolsInitialized)
                return;

            var config = plugin.Configuration;

            // Ensure the base directory exists
            if (!Directory.Exists(Configuration.BasePath))
            {
                Directory.CreateDirectory(Configuration.BasePath);
                reportProgress("Created base directory...");
            }

            // Ensure the tools directory exists
            if (!Directory.Exists(Configuration.ToolsPath))
            {
                Directory.CreateDirectory(Configuration.ToolsPath);
                reportProgress("Created tools directory...");
            }

            // Set tool paths
            FfmpegPath = Path.Combine(Configuration.ToolsPath, "ffmpeg.exe");
            YtdlpPath = Path.Combine(Configuration.ToolsPath, "yt-dlp.exe");

            // Download and extract tools if not already done
            if (!config.AreToolsDownloaded)
            {
                reportProgress("Downloading tools ZIP...");
                string tempZipPath = Path.Combine(Path.GetTempPath(), "tools.zip");
                await DownloadFileAsync(config.ToolsZipUrl, tempZipPath, reportProgress);

                reportProgress("Extracting tools ZIP...");
                ExtractZip(tempZipPath, Configuration.ToolsPath);
                File.Delete(tempZipPath); // Clean up the temporary ZIP file

                // Verify extraction
                if (File.Exists(FfmpegPath) && File.Exists(YtdlpPath))
                {
                    config.AreToolsDownloaded = true;
                    config.Save();
                    reportProgress("Tools downloaded and extracted successfully.");
                }
                else
                {
                    throw new FileNotFoundException("Failed to extract tools from the ZIP.");
                }
            }

            toolsInitialized = true;
        }

        /// <summary>
        /// Downloads a file from a URL to a local path with progress reporting.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="destinationPath">The local file path to save to.</param>
        /// <param name="reportProgress">Callback to report progress.</param>
        /// <returns></returns>
        private static async Task DownloadFileAsync(string url, string destinationPath, Action<string> reportProgress)
        {
            using var httpClient = new HttpClient();

            // Send HTTP request and get response as a stream
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && reportProgress != null;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var totalRead = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            do
            {
                var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0)
                {
                    isMoreToRead = false;
                    reportProgress?.Invoke("Download completed.");
                    continue;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, read));

                totalRead += read;
                if (canReportProgress)
                {
                    double progress = (double)totalRead / totalBytes * 100;
                    reportProgress?.Invoke($"Download Progress: {progress:F2}%");
                }
                else
                {
                    reportProgress?.Invoke($"Downloaded {totalRead} bytes...");
                }
            }
            while (isMoreToRead);
        }

        /// <summary>
        /// Extracts a ZIP file to the specified destination directory.
        /// </summary>
        /// <param name="zipPath">Path to the ZIP file.</param>
        /// <param name="extractPath">Destination directory.</param>
        private static void ExtractZip(string zipPath, string extractPath)
        {
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }

        /// <summary>
        /// Verifies the existence of tools. Throws exceptions if tools are missing.
        /// </summary>
        public static void VerifyTools(Plugin plugin)
        {
            if (toolsInitialized)
                return;

            var config = plugin.Configuration;
            var toolsDir = Configuration.ToolsPath;

            if (string.IsNullOrEmpty(toolsDir))
            {
                throw new Exception("Configuration.ToolsPath is not set. Cannot locate ffmpeg.exe and yt-dlp.exe.");
            }

            if (!Directory.Exists(toolsDir))
            {
                throw new DirectoryNotFoundException($"Tools directory not found: {toolsDir}");
            }

            FfmpegPath = Path.Combine(toolsDir, "ffmpeg.exe");
            YtdlpPath = Path.Combine(toolsDir, "yt-dlp.exe");
            
            if (!File.Exists(FfmpegPath))
            {
                throw new FileNotFoundException($"ffmpeg.exe not found in {toolsDir}");
            }

            if (!File.Exists(YtdlpPath))
            {
                throw new FileNotFoundException($"yt-dlp.exe not found in {toolsDir}");
            }

            config.AreToolsDownloaded = true;
            config.Save();

            toolsInitialized = true;
        }
    }
}
