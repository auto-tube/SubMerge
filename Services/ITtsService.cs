using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
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
        /// Should be called during application initialization.
        /// </summary>
        /// <param name="accessKeyId">AWS Access Key ID.</param>
        /// <param name="secretAccessKey">AWS Secret Access Key.</param>
        /// <param name="regionName">AWS Region (e.g., "us-east-1").</param>
        void Configure(string? accessKeyId, string? secretAccessKey, string? regionName);

        /// <summary>
        /// Asynchronously retrieves a list of available voices for a specific language (optional).
        /// </summary>
        /// <param name="languageCode">The language code (e.g., "en-US").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A list of voice IDs, or an empty list if retrieval fails or is not supported.</returns>
        Task<List<string>> GetAvailableVoicesAsync(string languageCode = "en-US", CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously synthesizes speech from text and saves it to an audio file.
        /// </summary>
        /// <param name="text">The text to synthesize.</param>
        /// <param name="voiceId">The voice ID to use (e.g., "Joanna").</param>
        /// <param name="outputFilePath">The full path where the output audio file (e.g., MP3) should be saved.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if synthesis was successful, false otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not configured/available.</exception>
        /// <exception cref="ArgumentException">Thrown for invalid input parameters.</exception>
        Task<bool> SynthesizeSpeechAsync(string text, string voiceId, string outputFilePath, CancellationToken cancellationToken = default);
    }
}