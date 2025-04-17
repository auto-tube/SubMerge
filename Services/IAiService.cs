using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines the contract for interacting with AI models (e.g., Google Gemini).
    /// </summary>
    public interface IAiService
    {
        /// <summary>
        /// Gets a value indicating whether the AI service is configured and available.
        /// (e.g., API key is set).
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Configures the AI service. Relies on ADC or environment variables.
        /// Should be called during application initialization after loading settings/environment variables.
        /// </summary>
        void Configure(); // Removed apiKey parameter

        /// <summary>
        /// Asynchronously generates a video script based on a given prompt.
        /// </summary>
        /// <param name="prompt">The topic, niche, or idea for the script.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The generated script text, or null if generation failed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not configured/available.</exception>
        Task<string?> GenerateScriptAsync(string prompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously generates metadata (hashtags, tags, or titles) based on context.
        /// </summary>
        /// <param name="metadataType">The type of metadata to generate ("hashtags", "tags", "titles").</param>
        /// <param name="context">The text context (e.g., description, topic) to base generation on.</param>
        /// <param name="count">The desired number of metadata items.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A list of generated metadata strings, or null if generation failed.</returns>
        /// <exception cref="ArgumentException">Thrown if metadataType is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the service is not configured/available.</exception>
        Task<List<string>?> GenerateMetadataAsync(string metadataType, string context, int count, CancellationToken cancellationToken = default);
    }
}