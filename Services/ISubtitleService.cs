using System;
using System.Collections.Generic; // For List
using System.IO; // For IOException
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines the contract for generating subtitle files (e.g., SRT).
    /// </summary>
    public interface ISubtitleService
    {
        /// <summary>
        /// Asynchronously generates a timed subtitle file (SRT format) from text, optionally using speech marks for timing.
        /// </summary>
        /// <param name="scriptText">The full script text.</param>
        /// <param name="totalDuration">The total duration the subtitles should span (used if speech marks are not provided or incomplete).</param>
        /// <param name="outputSrtPath">The full path where the generated SRT file should be saved.</param>
        /// <param name="speechMarks">Optional list of speech marks from the TTS service for precise timing.</param>
        /// <param name="wordsPerMinute">Estimated reading speed (used only if speech marks are not provided).</param>
        /// <param name="maxCharsPerLine">Approximate maximum characters per subtitle line for formatting.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown for invalid input parameters.</exception>
        /// <exception cref="IOException">Thrown if file writing fails.</exception>
        Task GenerateSrtFileAsync(
            string scriptText,
            TimeSpan totalDuration,
            string outputSrtPath,
            List<SpeechMark>? speechMarks = null, // Added optional speech marks
            int wordsPerMinute = 150,
            int maxCharsPerLine = 42,
            CancellationToken cancellationToken = default);
    }
}