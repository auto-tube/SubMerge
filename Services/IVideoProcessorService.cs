using System;
using System.Collections.Generic; // For List
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
        /// <summary>
        /// Overall percentage completion (0.0 to 1.0) for the current operation or batch.
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Current processing time within the current FFmpeg operation. Null if not applicable.
        /// </summary>
        public TimeSpan? CurrentTime { get; set; }

        /// <summary>
        /// Total duration of the clip/video being processed by the current FFmpeg operation. Null if not applicable.
        /// </summary>
        public TimeSpan? TotalDuration { get; set; }

        /// <summary>
        /// A message describing the current status or step.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Estimated time remaining for the current operation or batch. Null if cannot be estimated.
        /// </summary>
        public TimeSpan? EstimatedRemaining { get; set; }

        /// <summary>
        /// Index of the current item being processed in a batch (0-based). Null if not a batch operation.
        /// </summary>
        public int? CurrentItemIndex { get; set; }

        /// <summary>
        /// Total number of items in the batch. Null if not a batch operation.
        /// </summary>
        public int? TotalItems { get; set; }
    }

    /// <summary>
    /// Represents a detected scene change.
    /// </summary>
    public class SceneChangeInfo
    {
        /// <summary>
        /// The timestamp (in seconds) where the scene change occurs.
        /// </summary>
        public double TimestampSeconds { get; set; }
        /// <summary>
        /// A score indicating the likelihood or intensity of the scene change (if available).
        /// </summary>
        public double? Score { get; set; } // Optional score
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
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Asynchronously attempts to locate and verify FFmpeg and FFprobe executables.
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
        /// <param name="removeAudio">If true, removes the audio track from the clip.</param>
        /// <param name="mirrorVideo">If true, applies a horizontal flip to the video.</param>
        /// <param name="enhanceVideo">If true, applies basic video enhancement filters.</param>
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
            bool removeAudio,      // Added
            bool mirrorVideo,      // Added
            bool enhanceVideo,     // Added
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
            string subtitlePath,
            string outputPath,
            string subtitleAlignment,
            string subtitleFontName,
            System.Windows.Media.Color subtitleFontColor,           // Changed type
            System.Windows.Media.Color subtitleOutlineColor,     // Changed type
            int subtitleOutlineThickness,
            bool useSubtitleBackgroundBox,
            System.Windows.Media.Color subtitleBackgroundColor,
            bool applyBackgroundBlur,           // Added
            bool applyBackgroundGrayscale,     // Added
            IProgress<VideoProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously detects scene changes in a video file.
        /// </summary>
        /// <param name="inputFile">Path to the input video file.</param>
        /// <param name="threshold">Scene detection threshold (e.g., 0.3 to 0.5). Lower values detect more changes.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A list of detected scene change timestamps (in seconds), or null if detection fails.</returns>
        /// <exception cref="InvalidOperationException">Thrown if FFmpeg is not available.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the input file does not exist.</exception>
        Task<List<SceneChangeInfo>?> DetectScenesAsync(string inputFile, double threshold = 0.4, CancellationToken cancellationToken = default);

    }
}