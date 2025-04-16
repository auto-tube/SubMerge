using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Represents a single speech mark with timing and value.
    /// </summary>
    public class SpeechMark
    {
        public int Time { get; set; } // Time offset in milliseconds
        public string Type { get; set; } = string.Empty; // e.g., "sentence", "word", "viseme", "ssml"
        public int? Start { get; set; } // Start offset in bytes (for word/viseme)
        public int? End { get; set; } // End offset in bytes (for word/viseme)
        public string Value { get; set; } = string.Empty; // e.g., the word or sentence text
    }

    /// <summary>
    /// Holds the result of a speech synthesis operation, including the audio path and speech marks.
    /// </summary>
    public class SynthesisResult
    {
        public bool Success { get; set; }
        public string? OutputFilePath { get; set; }
        public List<SpeechMark>? SpeechMarks { get; set; }
        public string? ErrorMessage { get; set; }
    }


    /// <summary>
    /// Defines the contract for Text-to-Speech services.
    /// </summary>
    public interface ITtsService
    {
        /// <summary>
        /// Gets a value indicating whether the TTS service is configured and available.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Configures the TTS service, typically with cloud credentials.
        /// </summary>
        void Configure(string? accessKeyId, string? secretAccessKey, string? regionName);

        /// <summary>
        /// Asynchronously retrieves a list of available voices for a specific language (optional).
        /// </summary>
        Task<List<string>> GetAvailableVoicesAsync(string languageCode = "en-US", CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously synthesizes speech from text and saves it to an audio file, optionally returning speech marks.
        /// </summary>
        /// <returns>A SynthesisResult object indicating success/failure and containing the output path and speech marks (if successful).</returns>
        Task<SynthesisResult> SynthesizeSpeechAsync(
            string text,
            string voiceId,
            string outputFilePath,
            bool includeSpeechMarks = true, // Default to true to get marks
            CancellationToken cancellationToken = default);
    }
}