using Amazon; // For RegionEndpoint
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime; // For BasicAWSCredentials, AWSCredentials
using AutoTubeWpf.Services; // For ILoggerService, SpeechMark, SynthesisResult
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json; // For parsing speech marks
using System.Threading;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Implements ITtsService using AWS Polly.
    /// </summary>
    public class PollyTtsService : ITtsService, IDisposable
    {
        private readonly ILoggerService _logger;
        private AmazonPollyClient? _pollyClient;
        private bool _isConfigured = false;

        public bool IsAvailable => this._isConfigured && this._pollyClient != null;

        public PollyTtsService(ILoggerService logger)
        {
            this._logger = logger;
            this._logger.LogInfo("PollyTtsService created.");
        }

        public void Configure(string? accessKeyId, string? secretAccessKey, string? regionName)
        {
            this._isConfigured = false;
            this._pollyClient?.Dispose();
            this._pollyClient = null;

            try
            {
                AWSCredentials? credentials = null;
                RegionEndpoint? region = null;
                string authMethod = "Default Chain (Profile/IAM Role/Env Vars)";

                if (!string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey))
                {
                    credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
                    authMethod = "Explicit Keys";
                    this._logger.LogInfo("Using explicit AWS credentials for Polly.");
                }
                else { this._logger.LogInfo("Explicit AWS credentials not provided, attempting default chain for Polly."); }

                if (!string.IsNullOrWhiteSpace(regionName))
                {
                    try { region = RegionEndpoint.GetBySystemName(regionName); this._logger.LogInfo($"Using specified AWS region for Polly: {region.SystemName}"); }
                    catch (ArgumentException) { this._logger.LogWarning($"Invalid AWS region name specified: '{regionName}'. Falling back to default region resolution."); region = null; }
                }
                else { this._logger.LogInfo("AWS region not specified, relying on SDK default for Polly."); }

                if (credentials != null && region != null) { this._pollyClient = new AmazonPollyClient(credentials, region); }
                else if (credentials != null) { this._pollyClient = new AmazonPollyClient(credentials); }
                else if (region != null) { this._pollyClient = new AmazonPollyClient(region); }
                else { this._pollyClient = new AmazonPollyClient(); } // Use default constructor (credentials/region resolved via SDK chain)

                this._isConfigured = true;
                this._logger.LogInfo($"PollyTtsService configured. Authentication method: {authMethod}. Region: {this._pollyClient.Config.RegionEndpoint?.SystemName ?? "SDK Default"}");
            }
            catch (Exception ex)
            {
                this._logger.LogError($"Failed to configure PollyTtsService (AmazonPollyClient creation failed): {ex.Message}", ex);
                this._isConfigured = false;
                this._pollyClient = null;
            }
        }

        public async Task<List<string>> GetAvailableVoicesAsync(string languageCode = "en-US", CancellationToken cancellationToken = default)
        {
            if (!this.IsAvailable) { this._logger.LogWarning("Cannot get voices: Polly service is not available/configured."); return new List<string>(); }

            this._logger.LogDebug($"Getting Polly voices for language: {languageCode}");
            var voices = new List<string>();
            try
            {
                var request = new DescribeVoicesRequest { LanguageCode = languageCode };
                DescribeVoicesResponse response;
                do
                {
                    // IsAvailable check at the start ensures _pollyClient is not null here
                    response = await this._pollyClient!.DescribeVoicesAsync(request, cancellationToken); // Added ! to satisfy CS8602
                    voices.AddRange(response.Voices.Select(v => v.Id.Value));
                    request.NextToken = response.NextToken;
                } while (!string.IsNullOrEmpty(response.NextToken) && !cancellationToken.IsCancellationRequested);
                this._logger.LogInfo($"Retrieved {voices.Count} Polly voices for {languageCode}.");
                return voices;
            }
            catch (OperationCanceledException) { this._logger.LogInfo("GetAvailableVoicesAsync cancelled."); return new List<string>(); }
            catch (Exception ex) { this._logger.LogError($"Error getting Polly voices: {ex.Message}", ex); return new List<string>(); }
        }

        // Updated SynthesizeSpeechAsync implementation
        public async Task<SynthesisResult> SynthesizeSpeechAsync(
            string text,
            string voiceId,
            string outputFilePath,
            bool includeSpeechMarks = true, // Default to true
            CancellationToken cancellationToken = default)
        {
            var result = new SynthesisResult { Success = false, OutputFilePath = outputFilePath };
            if (!this.IsAvailable)
            {
                result.ErrorMessage = "PollyTtsService is not configured or failed to initialize.";
                this._logger.LogError($"Cannot synthesize speech: {result.ErrorMessage}");
                return result; // Return failure result instead of throwing
            }
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text cannot be empty.", nameof(text));
            if (string.IsNullOrWhiteSpace(voiceId)) throw new ArgumentException("Voice ID cannot be empty.", nameof(voiceId));
            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentException("Output file path cannot be empty.", nameof(outputFilePath));

            this._logger.LogInfo($"Synthesizing speech to '{outputFilePath}' using voice '{voiceId}'. IncludeSpeechMarks: {includeSpeechMarks}. Text length: {text.Length}");

            MemoryStream? speechMarksStream = null;
            try
            {
                string? outputDir = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

                var request = new SynthesizeSpeechRequest
                {
                    Text = text,
                    VoiceId = VoiceId.FindValue(voiceId), // Use FindValue to get VoiceId object from string
                    OutputFormat = OutputFormat.Mp3,
                    // Request speech marks if needed
                    SpeechMarkTypes = includeSpeechMarks ? new List<string> { SpeechMarkType.Sentence, SpeechMarkType.Word } : new List<string>()
                };

                // IsAvailable check at the start ensures _pollyClient is not null here
                SynthesizeSpeechResponse response = await this._pollyClient!.SynthesizeSpeechAsync(request, cancellationToken); // Added ! to satisfy CS8602

                // Write audio stream
                using (var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.AudioStream.CopyToAsync(fileStream, cancellationToken);
                }
                this._logger.LogDebug($"Audio stream saved to '{outputFilePath}'.");

                result.Success = true;

                // Process speech marks if requested and available
                if (includeSpeechMarks && response.AudioStream.Length > 0) // Check AudioStream length as SpeechMarks stream might be separate
                {
                    // Note: Polly requires a separate request to get speech marks (OutputFormat.Json).
                    // This is less efficient than getting both in one call if the API supported it.
                    this._logger.LogDebug("Requesting speech marks (requires separate API call)...");
                    var marksRequest = new SynthesizeSpeechRequest
                    {
                        Text = text,
                        VoiceId = VoiceId.FindValue(voiceId), // Re-use the found VoiceId
                        OutputFormat = OutputFormat.Json, // Request JSON for speech marks
                        SpeechMarkTypes = new List<string> { SpeechMarkType.Sentence, SpeechMarkType.Word } // Must specify types even for JSON format
                    };
                    // IsAvailable check at the start ensures _pollyClient is not null here
                    SynthesizeSpeechResponse marksResponse = await this._pollyClient.SynthesizeSpeechAsync(marksRequest, cancellationToken);

                    speechMarksStream = new MemoryStream();
                    await marksResponse.AudioStream.CopyToAsync(speechMarksStream, cancellationToken);
                     speechMarksStream.Position = 0; // Reset stream position

                     result.SpeechMarks = this.ParseSpeechMarks(speechMarksStream); // Explicit this.
                     this._logger.LogInfo($"Parsed {result.SpeechMarks?.Count ?? 0} speech marks.");
                }

                this._logger.LogInfo($"Successfully synthesized speech (Success: {result.Success}).");
                return result;
            }
            catch (OperationCanceledException)
            {
                 result.ErrorMessage = "Speech synthesis cancelled.";
                 this._logger.LogInfo($"Speech synthesis cancelled for output '{outputFilePath}'.");
                 try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch (Exception delEx) { this._logger.LogWarning($"Failed to delete partial TTS output file '{outputFilePath}' on cancellation: {delEx.Message}"); }
                 return result; // Return result indicating failure
            }
            catch (AmazonPollyException pollyEx)
            {
                 result.ErrorMessage = $"AWS Polly error: {pollyEx.Message} (Code: {pollyEx.ErrorCode})";
                 this._logger.LogError($"AWS Polly error synthesizing speech: {pollyEx.Message} (Code: {pollyEx.ErrorCode})", pollyEx);
                 return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
                this._logger.LogError($"Unexpected error synthesizing speech: {ex.Message}", ex);
                 try { if (File.Exists(outputFilePath)) File.Delete(outputFilePath); } catch (Exception delEx) { this._logger.LogWarning($"Failed to delete partial TTS output file '{outputFilePath}' after error: {delEx.Message}"); }
                return result;
            }
            finally
            {
                 speechMarksStream?.Dispose(); // Dispose the memory stream if used
            }
        }

        // Helper to parse the JSON lines speech mark stream
        private List<SpeechMark> ParseSpeechMarks(Stream jsonStream)
        {
            var marks = new List<SpeechMark>();
            try
            {
                using (var reader = new StreamReader(jsonStream, System.Text.Encoding.UTF8))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            // Deserialize each line as a SpeechMark object
                            var mark = JsonSerializer.Deserialize<SpeechMark>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (mark != null)
                            {
                                marks.Add(mark);
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                             this._logger.LogWarning($"Failed to parse speech mark line: '{line}'. Error: {jsonEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 this._logger.LogError($"Error reading speech mark stream: {ex.Message}", ex);
            }
            return marks;
        }

        #region IDisposable Implementation

        private bool _disposed = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    this._pollyClient?.Dispose(); // Explicit this.
                    this._pollyClient = null;     // Explicit this.
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                _disposed = true;
            }
        }

        // Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~PollyTtsService()
        // {
        //     // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //     Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // Uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}