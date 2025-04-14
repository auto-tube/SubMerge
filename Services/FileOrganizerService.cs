using System;
using System.IO;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Implements IFileOrganizerService to move output files into structured directories.
    /// </summary>
    public class FileOrganizerService : IFileOrganizerService
    {
        private readonly ILoggerService _logger;

        public FileOrganizerService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("FileOrganizerService created.");
        }

        public async Task<string> OrganizeOutputFileAsync(string processedFilePath, string originalInputPath, string baseOutputPath, string outputType = "output")
        {
            if (string.IsNullOrWhiteSpace(processedFilePath) || !File.Exists(processedFilePath))
            {
                _logger.LogWarning($"Cannot organize file: Processed file path is invalid or file does not exist ('{processedFilePath}').");
                throw new ArgumentException("Processed file path is invalid or file does not exist.", nameof(processedFilePath));
            }
            if (string.IsNullOrWhiteSpace(originalInputPath))
            {
                 _logger.LogWarning($"Cannot organize file '{processedFilePath}': Original input path is missing.");
                 throw new ArgumentException("Original input path cannot be empty.", nameof(originalInputPath));
            }
             if (string.IsNullOrWhiteSpace(baseOutputPath) || !Directory.Exists(baseOutputPath))
            {
                 _logger.LogWarning($"Cannot organize file '{processedFilePath}': Base output path is invalid or does not exist ('{baseOutputPath}').");
                 throw new ArgumentException("Base output path is invalid or does not exist.", nameof(baseOutputPath));
            }

            try
            {
                // 1. Determine the target directory structure
                // Structure: baseOutputPath / OriginalFileName_NoExt / OutputType / ProcessedFileName
                string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(originalInputPath);
                string typeSubfolder = SanitizeFolderName(outputType); // Sanitize type name for folder
                string targetDirectory = Path.Combine(baseOutputPath, originalFileNameWithoutExt, typeSubfolder);

                // 2. Create the target directory if it doesn't exist
                Directory.CreateDirectory(targetDirectory); // Creates all intermediate directories

                // 3. Determine the final path for the file
                string processedFileName = Path.GetFileName(processedFilePath);
                string targetFilePath = Path.Combine(targetDirectory, processedFileName);

                // 4. Handle potential naming conflicts (optional, overwrite for now)
                if (File.Exists(targetFilePath))
                {
                    _logger.LogWarning($"Target file '{targetFilePath}' already exists. Overwriting.");
                    // Or implement renaming logic:
                    // int count = 1;
                    // string baseName = Path.GetFileNameWithoutExtension(targetFilePath);
                    // string ext = Path.GetExtension(targetFilePath);
                    // while (File.Exists(targetFilePath))
                    // {
                    //     targetFilePath = Path.Combine(targetDirectory, $"{baseName}_{count++}{ext}");
                    // }
                }

                // 5. Move the file asynchronously (using Task.Run for potentially blocking I/O)
                _logger.LogDebug($"Moving '{processedFilePath}' to '{targetFilePath}'...");
                await Task.Run(() => File.Move(processedFilePath, targetFilePath, true)); // Overwrite = true

                _logger.LogInfo($"Successfully organized file to '{targetFilePath}'.");
                return targetFilePath; // Return the new path
            }
            catch (IOException ioEx)
            {
                 _logger.LogError($"IO error organizing file '{processedFilePath}': {ioEx.Message}", ioEx);
                 throw; // Re-throw IO exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error organizing file '{processedFilePath}': {ex.Message}", ex);
                // Don't throw for general errors, just return original path? Or re-throw?
                // Let's re-throw for now to indicate failure clearly.
                throw new Exception($"Failed to organize output file: {ex.Message}", ex);
            }
        }

        // Basic sanitization for folder names
        private string SanitizeFolderName(string name)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            string sanitized = name;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "_"); // Replace invalid chars with underscore
            }
            return sanitized.Trim(); // Remove leading/trailing whitespace
        }
    }
}