using System;
using System.IO; // Required for FileNotFoundException
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines progress information for video processing operations.
    /// </summary>
    public class VideoProcessingProgress
    {
        public double Percentage { get; set; } // 0.0 to 1.0
        public string Message { get; set; } = string.Empty;
        public TimeSpan? EstimatedRemaining { get; set; }
    }

    /// <summary>
    /// Defines the contract for interacting with video processing tools like FFmpeg/FFprobe.
    /// </summary>
    public interface IVideoProcessorService
    {
        /// <summary>
        /// Gets the full path to the detected FFmpeg executable. Returns null if not found/verified.
        /// </summary>
        string? FfmpegPath { get; }

        /// <summary>
        /// Gets the full path to the detected FFprobe executable. Returns null if not found/verified.
        /// </summary>
        string? FfprobePath { get; }

        /// <summary>
        /// Gets a value indicating whether FFmpeg and FFprobe have been successfully located and verified.
        /// This should be checked before attempting operations that require them.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Asynchronously attempts to locate and verify FFmpeg and FFprobe executables.
        /// This method should be called during application initialization.
        /// It updates the IsAvailable, FfmpegPath, and FfprobePath properties.
        /// </summary>
        /// <returns>True if both were found and verified successfully, false otherwise.</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Asynchronously extracts a clip from a video file.
        /// </summary>
        /// <param name="inputFile">Path to the input video file.</param>
        /// <param name="outputFile">Path for the output clip file.</param>
        /// <param name="start">Start time of the clip.</param>
        /// <param name="duration">Duration of the clip.</param>
        /// <param name="formatAsVertical">If true, formats the output as a vertical video (e.g., 9:16).</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if FFmpeg is not available.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the input file does not exist.</exception>
        /// <exception cref="Exception">Thrown if FFmpeg process fails.</exception>
        Task ExtractClipAsync(
            string inputFile,
            string outputFile,
            TimeSpan start,
            TimeSpan duration,
            bool formatAsVertical,
            IProgress<VideoProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets the duration of a video file using FFprobe.
        /// </summary>
        /// <param name="inputFile">Path to the input video file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The duration of the video, or null if it could not be determined.</returns>
        /// <exception cref="InvalidOperationException">Thrown if FFprobe is not available.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the input file does not exist.</exception>
        Task<TimeSpan?> GetVideoDurationAsync(string inputFile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously combines a background video, an audio track, and overlays subtitles.
        /// The output video will have the duration of the audio track.
        /// The background video will be looped and cropped/scaled to fit a vertical 9:16 format.
        /// </summary>
        /// <param name="backgroundVideoPath">Path to the background video file.</param>
        /// <param name="audioPath">Path to the audio file (e.g., MP3 from TTS).</param>
        /// <param name="subtitlePath">Path to the subtitle file (e.g., ASS or SRT).</param>
        /// <param name="outputPath">Path for the final output video file.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if FFmpeg/FFprobe is not available.</exception>
        /// <exception cref="FileNotFoundException">Thrown if input files do not exist.</exception>
        /// <exception cref="Exception">Thrown if FFmpeg process fails.</exception>
        Task CombineVideoAudioSubtitlesAsync(
            string backgroundVideoPath,
            string audioPath,
            string subtitlePath, // Assuming subtitles are pre-generated in a file
            string outputPath,
            IProgress<VideoProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default);

        // Add other methods as needed (e.g., scene detection)
    }
}