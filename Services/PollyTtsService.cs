using Amazon; // For RegionEndpoint
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime; // For BasicAWSCredentials, AWSCredentials
using AutoTubeWpf.Services; // For ILoggerService
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Implements ITtsService using AWS Polly.
    /// </summary>
    public class PollyTtsService : ITtsService
    {
        private readonly ILoggerService _logger;
        private AmazonPollyClient? _pollyClient;
        private bool _isConfigured = false;

        public bool IsAvailable => _isConfigured &amp;&amp; _pollyClient != null;

        public PollyTtsService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("PollyTtsService created.");
        }

        public void Configure(string? accessKeyId, string? secretAccessKey, string? regionName)
        {
            _isConfigured = false; // Reset status
            _pollyClient?.Dispose(); // Dispose previous client if any
            _pollyClient = null;

            try
            {
                AWSCredentials? credentials = null;
                RegionEndpoint? region = null;
                string authMethod = "Default Chain (Profile/IAM Role/Env Vars)";

                // 1. Explicit Credentials (highest priority if provided)
                if (!string.IsNullOrWhiteSpace(accessKeyId) &amp;&amp; !string.IsNullOrWhiteSpace(secretAccessKey))
                {
                    credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
                    authMethod = "Explicit Keys";
                    _logger.LogInfo("Using explicit AWS credentials for Polly.");
                }
                else
                {
                     _logger.LogInfo("Explicit AWS credentials not provided, attempting default chain for Polly.");
                }

                // 2. Region
                if (!string.IsNullOrWhiteSpace(regionName))
                {
                    try
                    {
                        region = RegionEndpoint.GetBySystemName(regionName);
                        _logger.LogInfo($"Using specified AWS region for Polly: {region.SystemName}");
                    }
                    catch (ArgumentException)
                    {
                         _logger.LogWarning($"Invalid AWS region name specified: '{regionName}'. Falling back to default region resolution.");
                         region = null; // Let SDK determine region if specified one is invalid
                    }
                }
                else
                {
                     _logger.LogInfo("AWS region not specified, relying on SDK default for Polly.");
                }


                // 3. Create Client
                if (credentials != null &amp;&amp; region != null)
                {
                    _pollyClient = new AmazonPollyClient(credentials, region);
                }
                else if (credentials != null) // Credentials provided, but region not (or invalid)
                {
                    _pollyClient = new AmazonPollyClient(credentials); // Rely on SDK for region
                }
                else if (region != null) // Region provided, but no explicit credentials
                {
                    _pollyClient = new AmazonPollyClient(region); // Use default credential chain + specified region
                }
                else // Neither provided (or region invalid)
                {
                    _pollyClient = new AmazonPollyClient(); // Use default credential chain + default region
                }

                // Simple check: If client created, assume configured for now.
                // A better check would be DescribeVoicesAsync, but let's keep it simple initially.
                _isConfigured = true;
                _logger.LogInfo($"PollyTtsService configured. Authentication method: {authMethod}. Region: {_pollyClient.Config.RegionEndpoint?.SystemName ?? "SDK Default"}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to configure PollyTtsService (AmazonPollyClient creation failed): {ex.Message}", ex);
                _isConfigured = false;
                _pollyClient = null;
            }
        }

        public async Task<List<string>> GetAvailableVoicesAsync(string languageCode = "en-US", CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("Cannot get voices: Polly service is not available/configured.");
                return new List<string>(); // Return empty list
            }

            _logger.LogDebug($"Getting Polly voices for language: {languageCode}");
            var voices = new List<string>();
            try
            {
                var request = new DescribeVoicesRequest { LanguageCode = languageCode };
                DescribeVoicesResponse response;
                do
                {
                    response = await _pollyClient!.DescribeVoicesAsync(request, cancellationToken);
                    voices.AddRange(response.Voices.Select(v => v.Id.Value)); // Assuming Id is the desired identifier
                    request.NextToken = response.NextToken;
                } while (!string.IsNullOrEmpty(response.NextToken) &amp;&amp; !cancellationToken.IsCancellationRequested);

                _logger.LogInfo($"Retrieved {voices.Count} Polly voices for {languageCode}.");
                return voices;
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo("GetAvailableVoicesAsync cancelled.");
                 return new List<string>(); // Return empty on cancel
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting Polly voices: {ex.Message}", ex);
                return new List<string>(); // Return empty list on error
            }
        }

        public async Task<bool> SynthesizeSpeechAsync(string text, string voiceId, string outputFilePath, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                _logger.LogError("Cannot synthesize speech: Polly service is not available/configured.");
                throw new InvalidOperationException("PollyTtsService is not configured or failed to initialize.");
            }
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text cannot be empty.", nameof(text));
            if (string.IsNullOrWhiteSpace(voiceId)) throw new ArgumentException("Voice ID cannot be empty.", nameof(voiceId));
            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentException("Output file path cannot be empty.", nameof(outputFilePath));

            _logger.LogInfo($"Synthesizing speech to '{outputFilePath}' using voice '{voiceId}'. Text length: {text.Length}");

            try
            {
                // Ensure output directory exists
                string? outputDir = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

                var request = new SynthesizeSpeechRequest
                {
                    Text = text,
                    VoiceId = (VoiceId)voiceId, // Cast string to Enum
                    OutputFormat = OutputFormat.Mp3 // Or OggVorbis, Pcm
                    // Engine = Engine.Neural // Optional: Use neural voices if desired and available
                };

                SynthesizeSpeechResponse response = await _pollyClient!.SynthesizeSpeechAsync(request, cancellationToken);

                // Write the audio stream to the output file
                using (var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.AudioStream.CopyToAsync(fileStream, cancellationToken);
                }

                _logger.LogInfo($"Successfully synthesized speech and saved to '{outputFilePath}'.");
                return true;
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo($"Speech synthesis cancelled for output '{outputFilePath}'.");
                 // Clean up potentially partial file
                 try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial TTS output file '{outputFilePath}' on cancellation: {delEx.Message}"); }
                 return false; // Indicate failure due to cancellation
            }
            catch (AmazonPollyException pollyEx)
            {
                 _logger.LogError($"AWS Polly error synthesizing speech: {pollyEx.Message} (Code: {pollyEx.ErrorCode})", pollyEx);
                 return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error synthesizing speech: {ex.Message}", ex);
                 // Clean up potentially partial file
                 try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch (Exception delEx) { _logger.LogWarning($"Failed to delete partial TTS output file '{outputFilePath}' after error: {delEx.Message}"); }
                return false;
            }
        }
    }
}