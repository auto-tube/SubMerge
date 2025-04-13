using AutoTubeWpf.Services; // For ILoggerService
using Google.Api.Gax.Grpc; // For ApiKeyCredentials
using Google.Cloud.AIPlatform.V1; // Using Vertex AI client for Gemini Pro
// Or potentially: using Google.Ai.Generativelanguage.V1Beta; if using that specific endpoint/package
using Grpc.Core; // For RpcException
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions; // For cleaning output
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Implements IAiService using Google's Gemini model via the Vertex AI API.
    /// </summary>
    public class GeminiService : IAiService
    {
        private readonly ILoggerService _logger;
        private PredictionServiceClient? _predictionServiceClient;
        private string? _apiKey; // Store the API key if provided directly
        private bool _isConfigured = false;

        // TODO: Update these with your actual project ID and location (region)
        private const string ProjectId = "your-gcp-project-id";
        private const string LocationId = "us-central1"; // e.g., us-central1
        private const string Publisher = "google";
        private const string Model = "gemini-1.0-pro-001"; // Or the specific Gemini model you want to use

        public bool IsAvailable => _isConfigured &amp;&amp; _predictionServiceClient != null;

        public GeminiService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("GeminiService created.");
        }

        public void Configure(string? apiKey)
        {
            _apiKey = apiKey; // Store key even if null/empty, client creation handles auth
            _isConfigured = false; // Reset configured state until client is created
            _predictionServiceClient = null; // Dispose existing client if any

            try
            {
                // Client creation handles authentication (ADC, GOOGLE_APPLICATION_CREDENTIALS, or API Key if supported by builder)
                var clientBuilder = new PredictionServiceClientBuilder
                {
                    Endpoint = $"{LocationId}-aiplatform.googleapis.com", // Standard Vertex AI endpoint
                    // If using API Key directly (check if builder supports this, might need custom headers/credentials)
                    // Credentials = !string.IsNullOrEmpty(_apiKey) ? new ApiKeyCredentials(_apiKey) : null
                };

                _predictionServiceClient = clientBuilder.Build();

                // A simple check to see if configuration seems okay (doesn't guarantee auth works yet)
                // A better check would be a small test call if possible without cost.
                _isConfigured = true;
                _logger.LogInfo($"GeminiService configured. Endpoint: {clientBuilder.Endpoint}. Project: {ProjectId}, Location: {LocationId}");

                // Note: Actual authentication happens on the first API call.
                // If GOOGLE_APPLICATION_CREDENTIALS env var is set, it will be used.
                // If running on GCP infra, Application Default Credentials (ADC) will be used.
                // If an API key was intended, ensure the builder/client handles it correctly.
                if (!string.IsNullOrEmpty(_apiKey))
                {
                     _logger.LogWarning("API Key provided to GeminiService.Configure, but direct API key usage with Vertex AI PredictionServiceClientBuilder might require specific setup (e.g., custom credentials or headers). Ensure your environment is configured for authentication (ADC, Env Var).");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to configure GeminiService (PredictionServiceClient creation failed): {ex.Message}", ex);
                _isConfigured = false;
                _predictionServiceClient = null;
            }
        }

        public async Task<string?> GenerateScriptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                _logger.LogError("Cannot generate script: GeminiService is not available/configured.");
                throw new InvalidOperationException("GeminiService is not configured or failed to initialize.");
            }

            _logger.LogInfo($"Generating script with prompt: '{prompt}'");

            // Construct the prompt for script generation
            string fullPrompt = $"Generate a concise and engaging YouTube Short script (around 150-200 words) suitable for text-to-speech narration based on the following topic/idea:\n\nTopic/Idea: \"{prompt}\"\n\nScript:";

            try
            {
                var predictionRequest = new PredictRequest
                {
                    EndpointAsEndpointName = EndpointName.FromProjectLocationPublisherModel(ProjectId, LocationId, Publisher, Model),
                    Instances = { Google.Protobuf.WellKnownTypes.Value.ForString(fullPrompt) },
                    // Add parameters if needed (e.g., temperature, max output tokens)
                    // Parameters = Google.Protobuf.WellKnownTypes.Value.ForStruct(new Google.Protobuf.WellKnownTypes.Struct { ... })
                };

                _logger.LogDebug("Sending prediction request to Vertex AI...");
                PredictResponse response = await _predictionServiceClient!.PredictAsync(predictionRequest, cancellationToken);
                _logger.LogDebug("Received prediction response from Vertex AI.");

                // Extract the generated text (assuming simple text output)
                // The structure of the response depends on the model. Inspect the response object.
                // This is a common pattern for text generation models:
                string? generatedText = response?.Predictions?.FirstOrDefault()?.ToString();

                if (string.IsNullOrWhiteSpace(generatedText))
                {
                    _logger.LogWarning("Gemini response was empty or whitespace.");
                    return null;
                }

                // Basic cleanup (remove potential quotes, trim)
                generatedText = generatedText.Trim('"', '\'', ' ', '\n', '\r');
                _logger.LogInfo($"Script generated successfully (Length: {generatedText.Length}).");
                return generatedText;

            }
            catch (RpcException rpcEx)
            {
                 _logger.LogError($"RPC error generating script: {rpcEx.Status}", rpcEx);
                 // Handle specific gRPC errors (e.g., authentication, quota)
                 throw new Exception($"AI service communication error: {rpcEx.Status.Detail}", rpcEx);
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo("Script generation cancelled.");
                 throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error generating script: {ex.Message}", ex);
                throw; // Re-throw unexpected errors
            }
        }

        public async Task<List<string>?> GenerateMetadataAsync(string metadataType, string context, int count, CancellationToken cancellationToken = default)
        {
             if (!IsAvailable)
            {
                _logger.LogError($"Cannot generate {metadataType}: GeminiService is not available/configured.");
                throw new InvalidOperationException("GeminiService is not configured or failed to initialize.");
            }
            if (string.IsNullOrWhiteSpace(context))
            {
                 throw new ArgumentException("Context cannot be empty.", nameof(context));
            }
            if (count <= 0)
            {
                 throw new ArgumentException("Count must be positive.", nameof(count));
            }

            string itemType;
            string formatInstruction;
            switch (metadataType.ToLowerInvariant())
            {
                case "hashtags":
                    itemType = "relevant hashtags (single words or short phrases, starting with #)";
                    formatInstruction = "Provide each hashtag on a new line, starting with #.";
                    break;
                case "tags":
                    itemType = "relevant tags/keywords (single words or short phrases)";
                    formatInstruction = "Provide each tag/keyword on a new line.";
                    break;
                case "titles":
                    itemType = "compelling and concise titles/headlines (under 70 characters each)";
                    formatInstruction = "Provide each title/headline on a new line.";
                    break;
                default:
                    throw new ArgumentException($"Invalid metadata type: {metadataType}", nameof(metadataType));
            }

            _logger.LogInfo($"Generating {count} {metadataType} based on context: '{context.Substring(0, Math.Min(context.Length, 50))}...'");

            // Construct the prompt
            string fullPrompt = $"Generate exactly {count} {itemType} based on the following context:\n\nContext: \"{context}\"\n\n{formatInstruction}\n\n{metadataType.CapitalizeFirst()}:"; // Capitalize first letter for output clarity

            try
            {
                 var predictionRequest = new PredictRequest
                {
                    EndpointAsEndpointName = EndpointName.FromProjectLocationPublisherModel(ProjectId, LocationId, Publisher, Model),
                    Instances = { Google.Protobuf.WellKnownTypes.Value.ForString(fullPrompt) },
                    // Parameters = ... // Adjust temperature, etc. if needed
                };

                _logger.LogDebug("Sending prediction request to Vertex AI...");
                PredictResponse response = await _predictionServiceClient!.PredictAsync(predictionRequest, cancellationToken);
                _logger.LogDebug("Received prediction response from Vertex AI.");

                string? generatedText = response?.Predictions?.FirstOrDefault()?.ToString();

                if (string.IsNullOrWhiteSpace(generatedText))
                {
                    _logger.LogWarning($"Gemini response for {metadataType} was empty or whitespace.");
                    return null;
                }

                // Parse the response (split by new lines, remove empty entries, trim)
                var results = generatedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(line => line.Trim('"', '\'', ' ', '*','-')) // Basic cleanup
                                           .Where(line => !string.IsNullOrWhiteSpace(line))
                                           .Take(count) // Ensure we don't exceed requested count
                                           .ToList();

                _logger.LogInfo($"Successfully generated {results.Count} {metadataType}.");
                return results;
            }
             catch (RpcException rpcEx)
            {
                 _logger.LogError($"RPC error generating {metadataType}: {rpcEx.Status}", rpcEx);
                 throw new Exception($"AI service communication error: {rpcEx.Status.Detail}", rpcEx);
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo($"{metadataType} generation cancelled.");
                 throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error generating {metadataType}: {ex.Message}", ex);
                throw;
            }
        }
    }

    // Helper extension method
    internal static class StringExtensions
    {
        internal static string CapitalizeFirst(this string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}