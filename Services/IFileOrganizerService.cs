using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines the contract for organizing output files.
    /// </summary>
    public interface IFileOrganizerService
    {
        /// <summary>
        /// Asynchronously organizes a processed output file into a structured directory based on the original input file.
        /// </summary>
        /// <param name="processedFilePath">The full path to the file that was just created (e.g., a clip or AI short).</param>
        /// <param name="originalInputPath">The full path to the original input video file this output was derived from.</param>
        /// <param name="baseOutputPath">The root directory where organized output should be placed (e.g., the user's selected default output folder).</param>
        /// <param name="outputType">A string indicating the type of output (e.g., "clip", "ai_short") to potentially use for subfolders.</param>
        /// <returns>The new full path of the organized file, or the original path if organization failed or wasn't applicable.</returns>
        /// <exception cref="System.IO.IOException">Thrown if file moving fails.</exception>
        /// <exception cref="System.ArgumentException">Thrown for invalid paths.</exception>
        Task<string> OrganizeOutputFileAsync(string processedFilePath, string originalInputPath, string baseOutputPath, string outputType = "output");
    }
}