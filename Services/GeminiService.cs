using AutoTubeWpf.Services; // For ILoggerService
using Google.Api.Gax; // Added for Expiration
using Google.Api.Gax.Grpc; // For CallSettings
using Google.Cloud.AIPlatform.V1; // Using Vertex AI client for Gemini Pro
// Or potentially: using Google.Ai.Generativelanguage.V1Beta; if using that specific endpoint/package
using Grpc.Core; // For RpcException, SslCredentials
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
    /// Relies on Application Default Credentials (ADC) or GOOGLE_APPLICATION_CREDENTIALS environment variable.
    /// </summary>
    public class GeminiService : IAiService
    {
        private readonly ILoggerService _logger;
        private PredictionServiceClient? _predictionServiceClient;
        // private string? _apiKey; // REMOVED API Key field
        private bool _isConfigured = false;

        // TODO: Update these with your actual project ID and location (region)
        private const string ProjectId = "my-project-name-123456"; // Updated Project ID
        private const string LocationId = "us-central1"; // Confirmed Location ID
        private const string Publisher = "google";
        private const string Model = "gemini-1.0-pro-001"; // Or the specific Gemini model you want to use

        public bool IsAvailable => _isConfigured && _predictionServiceClient != null;

        public GeminiService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("GeminiService created.");
        }

        // Modified Configure method - no longer accepts API key
        public void Configure() 
        {
            _logger.LogInfo($"GeminiService.Configure called."); // Log entry
            // _apiKey = null; // REMOVED
            _isConfigured = false; // Reset configured state until client is created
            _predictionServiceClient = null; // Dispose existing client if any

            try
            {
                _logger.LogDebug("Attempting to create PredictionServiceClientBuilder..."); // Log step
                // Client creation handles authentication (ADC, GOOGLE_APPLICATION_CREDENTIALS)
                var clientBuilder = new PredictionServiceClientBuilder
                {
                    Endpoint = $"{LocationId}-aiplatform.googleapis.com", // Standard Vertex AI endpoint
                };

                // REMOVED API Key specific logic for ChannelCredentials

                _logger.LogDebug("Attempting to build PredictionServiceClient..."); // Log step
                _predictionServiceClient = clientBuilder.Build(); 
                _logger.LogInfo("PredictionServiceClient built successfully."); // Log success

                _isConfigured = true; // Set configured flag ONLY after successful build
                _logger.LogInfo($"GeminiService configuration successful. IsAvailable: {IsAvailable}. Endpoint: {clientBuilder.Endpoint}. Project: {ProjectId}, Location: {LocationId}"); // Log final state

                // Note: Authentication happens via ADC or GOOGLE_APPLICATION_CREDENTIALS during client build.
                _logger.LogInfo("Attempting authentication via Application Default Credentials (ADC) or GOOGLE_APPLICATION_CREDENTIALS environment variable.");

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
                    // Parameters = Google.Protobuf.WellKnownTypes.Value.ForStruct(new Google.Protobuf.WellKnownTypes.Struct { ... })
                };

                _logger.LogDebug("Sending prediction request to Vertex AI...");
                
                // --- MODIFIED: Remove API Key header logic, add timeout ---
                var finalCallSettings = CallSettings.FromCancellationToken(cancellationToken)
                                                    .WithExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(30))); // 30-second timeout
                // --- END MODIFIED ---

                _logger.LogDebug("Calling PredictAsync with timeout..."); 
                PredictResponse response = await _predictionServiceClient!.PredictAsync(predictionRequest, finalCallSettings); 
                _logger.LogDebug("Received prediction response from Vertex AI.");

                string? generatedText = response?.Predictions?.FirstOrDefault()?.ToString();

                if (string.IsNullOrWhiteSpace(generatedText))
                {
                    _logger.LogWarning("Gemini response was empty or whitespace.");
                    return null;
                }

                generatedText = generatedText.Trim('"', '\'', ' ', '\n', '\r');
                _logger.LogInfo($"Script generated successfully (Length: {generatedText.Length}).");
                return generatedText;

            }
            catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.DeadlineExceeded) 
            {
                 _logger.LogError($"RPC error generating script: Call timed out after 30 seconds. Status: {rpcEx.Status}", rpcEx);
                 throw new Exception($"AI service call timed out. Check network or API status.", rpcEx);
            }
            catch (RpcException rpcEx)
            {
                 _logger.LogError($"RPC error generating script: {rpcEx.Status}", rpcEx);
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
                throw; 
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

            string fullPrompt = $"Generate exactly {count} {itemType} based on the following context:\n\nContext: \"{context}\"\n\n{formatInstruction}\n\n{metadataType.CapitalizeFirst()}:"; 

            try
            {
                 var predictionRequest = new PredictRequest
                {
                    EndpointAsEndpointName = EndpointName.FromProjectLocationPublisherModel(ProjectId, LocationId, Publisher, Model),
                    Instances = { Google.Protobuf.WellKnownTypes.Value.ForString(fullPrompt) },
                    // Parameters = ... 
                };

                _logger.LogDebug("Sending prediction request to Vertex AI...");
                
                // --- MODIFIED: Remove API Key header logic, add timeout ---
                var finalCallSettings = CallSettings.FromCancellationToken(cancellationToken)
                                                    .WithExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(30))); // 30-second timeout
                // --- END MODIFIED ---

                _logger.LogDebug("Calling PredictAsync with timeout..."); 
                PredictResponse response = await _predictionServiceClient!.PredictAsync(predictionRequest, finalCallSettings); 
                _logger.LogDebug("Received prediction response from Vertex AI.");

                string? generatedText = response?.Predictions?.FirstOrDefault()?.ToString();

                if (string.IsNullOrWhiteSpace(generatedText))
                {
                    _logger.LogWarning($"Gemini response for {metadataType} was empty or whitespace.");
                    return null;
                }

                var results = generatedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(line => line.Trim('"', '\'', ' ', '*','-')) 
                                           .Where(line => !string.IsNullOrWhiteSpace(line))
                                           .Take(count) 
                                           .ToList();

                _logger.LogInfo($"Successfully generated {results.Count} {metadataType}.");
                return results;
            }
             catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.DeadlineExceeded) 
            {
                 _logger.LogError($"RPC error generating {metadataType}: Call timed out after 30 seconds. Status: {rpcEx.Status}", rpcEx);
                 throw new Exception($"AI service call timed out. Check network or API status.", rpcEx);
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