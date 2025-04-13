using AutoTubeWpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic; // For Polly voices list
using System.Globalization; // For CultureInfo in SRT
using System.IO;
using System.Linq; // For FirstOrDefault
using System.Text; // For StringBuilder in SRT
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks;
using System.Windows; // For MessageBox - replace with DialogService later

namespace AutoTubeWpf.ViewModels
{
    public partial class AiShortViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly IAiService _aiService;
        private readonly ITtsService _ttsService;
        private readonly IVideoProcessorService _videoProcessorService; // Added Video Processor service
        private readonly IConfigurationService _configurationService; // Added Config service
        private CancellationTokenSource? _scriptGenerationCts;
        private CancellationTokenSource? _shortGenerationCts;

        // --- UI Bound Properties ---

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private string? _backgroundVideoPath;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private string? _outputFolderPath; // Separate output folder for AI shorts

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private string? _scriptText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private string? _selectedPollyVoice; // e.g., "Joanna"

        [ObservableProperty]
        private int _fontSize = 48; // Note: Font size not directly used by FFmpeg subtitles filter, styling is via ASS/SRT tags or filter options

        [ObservableProperty]
        private List<string> _availablePollyVoices = new List<string> { "Joanna", "Matthew", "Salli", "Ivy", "Kendra", "Justin" };

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateScriptCommand))]
        private string? _scriptPrompt;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateScriptCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private bool _isGeneratingScript = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateScriptCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private bool _isGeneratingShort = false;


        // --- Constructor ---
        public AiShortViewModel(
            ILoggerService loggerService,
            IAiService aiService,
            ITtsService ttsService,
            IVideoProcessorService videoProcessorService, // Added VP service
            IConfigurationService configurationService) // Added Config service
        {
            _logger = loggerService;
            _aiService = aiService;
            _ttsService = ttsService;
            _videoProcessorService = videoProcessorService; // Store VP service
            _configurationService = configurationService; // Store Config service

            _logger.LogInfo("AiShortViewModel initialized.");
            // Set default output path from config if not already set by user interaction
            if (string.IsNullOrWhiteSpace(OutputFolderPath))
            {
                 OutputFolderPath = _configurationService.CurrentSettings.DefaultOutputPath;
                 _logger.LogDebug($"Default AI Short output folder set from config: {OutputFolderPath}");
            }
            // Fetch Polly voices asynchronously
            _ = LoadVoicesAsync();
            SelectedPollyVoice = AvailablePollyVoices.FirstOrDefault();
        }

        // --- Methods ---

        private async Task LoadVoicesAsync()
        {
            if (!_ttsService.IsAvailable) return;

            _logger.LogDebug("Attempting to load Polly voices...");
            try
            {
                var voices = await _ttsService.GetAvailableVoicesAsync();
                if (voices != null &amp;&amp; voices.Any())
                {
                    AvailablePollyVoices = voices;
                    SelectedPollyVoice = AvailablePollyVoices.FirstOrDefault();
                    _logger.LogInfo($"Loaded {AvailablePollyVoices.Count} Polly voices.");
                }
                else
                {
                     _logger.LogWarning("Failed to load Polly voices or no voices returned.");
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError("Error loading Polly voices.", ex);
            }
        }


        // --- Commands ---

        [RelayCommand]
        private void BrowseBackgroundVideo()
        {
            _logger.LogDebug("BrowseBackgroundVideo command executed.");
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Background Video File",
                Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv|All Files|*.*",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                BackgroundVideoPath = dialog.FileName;
                _logger.LogInfo($"Background video selected: {BackgroundVideoPath}");
            }
        }

        [RelayCommand]
        private void BrowseOutputFolder()
        {
            _logger.LogDebug("BrowseOutputFolder (AI Short) command executed.");
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = "Select Output Folder for AI Shorts",
                UseDescriptionForTitle = true
            };
             if (!string.IsNullOrWhiteSpace(OutputFolderPath) &amp;&amp; Directory.Exists(OutputFolderPath))
            {
                dialog.SelectedPath = OutputFolderPath;
            }
             else // Fallback to default output or user profile
            {
                 string defaultOutput = _configurationService?.CurrentSettings?.DefaultOutputPath ?? "";
                 dialog.SelectedPath = !string.IsNullOrWhiteSpace(defaultOutput) && Directory.Exists(defaultOutput)
                                       ? defaultOutput
                                       : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (dialog.ShowDialog() == true)
            {
                OutputFolderPath = dialog.SelectedPath;
                _logger.LogInfo($"AI Short output folder selected: {OutputFolderPath}");
            }
        }


        [RelayCommand(CanExecute = nameof(CanGenerateScript))]
        private async Task GenerateScriptAsync()
        {
            if (string.IsNullOrWhiteSpace(ScriptPrompt))
            {
                 MessageBox.Show("Please enter a prompt for script generation.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }

            _logger.LogInfo("GenerateScript command executed.");
            _scriptGenerationCts = new CancellationTokenSource();
            IsGeneratingScript = true;
            GenerateScriptCommand.NotifyCanExecuteChanged();

            try
            {
                ScriptText = "Generating script...";
                string? generatedScript = await _aiService.GenerateScriptAsync(ScriptPrompt, _scriptGenerationCts.Token);

                if (generatedScript != null)
                {
                    ScriptText = generatedScript;
                    _logger.LogInfo("Script generated successfully.");
                    MessageBox.Show("Script generated successfully and placed in the text box.", "Script Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ScriptText = "Script generation failed or returned empty.";
                    _logger.LogWarning("Script generation failed or returned empty.");
                     MessageBox.Show("Script generation failed or returned an empty result. Check logs for details.", "Generation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                ScriptText = "Script generation cancelled.";
                _logger.LogInfo("Script generation was cancelled by the user.");
                 MessageBox.Show("Script generation was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ioex)
            {
                 ScriptText = $"Script generation failed: {ioex.Message}";
                 _logger.LogError($"Script generation failed: {ioex.Message}", ioex);
                 MessageBox.Show($"Script generation failed:\n{ioex.Message}\n\nPlease ensure the AI Service is configured correctly in Settings.", "Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                ScriptText = $"Script generation failed: {ex.Message}";
                _logger.LogError("An unexpected error occurred during script generation.", ex);
                MessageBox.Show($"An unexpected error occurred during script generation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsGeneratingScript = false;
                _scriptGenerationCts?.Dispose();
                _scriptGenerationCts = null;
                GenerateScriptCommand.NotifyCanExecuteChanged();
            }
        }
        private bool CanGenerateScript()
        {
            return !IsGeneratingScript &amp;&amp; !IsGeneratingShort &amp;&amp; !string.IsNullOrWhiteSpace(ScriptPrompt) &amp;&amp; _aiService.IsAvailable;
        }


        [RelayCommand(CanExecute = nameof(CanGenerateAiShort))]
        private async Task GenerateAiShortAsync()
        {
            _logger.LogInfo("GenerateAiShort command executed.");

            // --- Input Validation ---
            if (!CanGenerateAiShort()) // Re-check CanExecute logic here for robustness
            {
                 MessageBox.Show("Cannot generate AI Short. Please ensure all inputs are valid and required services (AI, TTS, Video Processor) are available and configured.", "Prerequisites Not Met", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }

            _shortGenerationCts = new CancellationTokenSource();
            IsGeneratingShort = true;
            GenerateAiShortCommand.NotifyCanExecuteChanged();

            string tempAudioFilePath = Path.Combine(Path.GetTempPath(), $"autotube_tts_{Guid.NewGuid()}.mp3");
            string tempSrtFilePath = Path.Combine(Path.GetTempPath(), $"autotube_sub_{Guid.NewGuid()}.srt");
            string finalOutputPath = Path.Combine(OutputFolderPath!, $"AI_Short_{Path.GetFileNameWithoutExtension(BackgroundVideoPath)}_{DateTime.Now:yyyyMMddHHmmss}.mp4");

            try
            {
                // 1. Synthesize Speech
                _logger.LogDebug($"Synthesizing speech to temp file: {tempAudioFilePath}");
                bool ttsSuccess = await _ttsService.SynthesizeSpeechAsync(ScriptText!, SelectedPollyVoice!, tempAudioFilePath, _shortGenerationCts.Token);
                if (!ttsSuccess) throw new Exception("TTS synthesis failed."); // Throw exception to handle in catch block
                _logger.LogInfo("TTS synthesis successful.");

                // 2. Get Audio Duration (needed for SRT timing)
                TimeSpan? audioDuration = await _videoProcessorService.GetVideoDurationAsync(tempAudioFilePath, _shortGenerationCts.Token);
                if (audioDuration == null) throw new Exception("Failed to get duration of generated audio file.");
                _logger.LogInfo($"Generated audio duration: {audioDuration.Value}");

                // 3. Generate Subtitle File (Simple SRT for now)
                _logger.LogDebug($"Generating temporary SRT file: {tempSrtFilePath}");
                await GenerateSrtFileAsync(ScriptText!, audioDuration.Value, tempSrtFilePath, _shortGenerationCts.Token);
                _logger.LogInfo("Temporary SRT file generated.");

                // 4. Combine Video, Audio, Subtitles
                _logger.LogDebug($"Combining inputs for output: {finalOutputPath}");
                // TODO: Implement progress reporting from Combine method
                await _videoProcessorService.CombineVideoAudioSubtitlesAsync(
                     BackgroundVideoPath!,
                     tempAudioFilePath,
                     tempSrtFilePath,
                     finalOutputPath,
                     null, // No progress reporter for now
                     _shortGenerationCts.Token);

                _logger.LogInfo("AI Short generation finished successfully.");
                MessageBox.Show($"AI Short Generation Complete!\nOutput: {finalOutputPath}", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo("AI Short generation was cancelled.");
                 MessageBox.Show("AI Short generation was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ioex)
            {
                 _logger.LogError($"AI Short generation failed due to service issue: {ioex.Message}", ioex);
                 MessageBox.Show($"AI Short generation failed:\n{ioex.Message}\n\nPlease ensure required services (TTS, Video Processor, AI) are configured correctly.", "Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError("An unexpected error occurred during AI Short generation.", ex);
                MessageBox.Show($"An unexpected error occurred during AI Short generation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Clean up temporary files
                try { if (File.Exists(tempAudioFilePath)) File.Delete(tempAudioFilePath); } catch (Exception ex) { _logger.LogWarning($"Failed to delete temp audio file '{tempAudioFilePath}': {ex.Message}"); }
                try { if (File.Exists(tempSrtFilePath)) File.Delete(tempSrtFilePath); } catch (Exception ex) { _logger.LogWarning($"Failed to delete temp SRT file '{tempSrtFilePath}': {ex.Message}"); }

                IsGeneratingShort = false;
                _shortGenerationCts?.Dispose();
                _shortGenerationCts = null;
                GenerateAiShortCommand.NotifyCanExecuteChanged();
            }
        }
        private bool CanGenerateAiShort()
        {
            return !IsGeneratingScript &amp;&amp; !IsGeneratingShort
                &amp;&amp; !string.IsNullOrWhiteSpace(BackgroundVideoPath) &amp;&amp; File.Exists(BackgroundVideoPath) // Check file exists
                &amp;&amp; !string.IsNullOrWhiteSpace(OutputFolderPath) &amp;&amp; Directory.Exists(OutputFolderPath) // Check dir exists
                &amp;&amp; !string.IsNullOrWhiteSpace(ScriptText)
                &amp;&amp; !string.IsNullOrWhiteSpace(SelectedPollyVoice)
                &amp;&amp; _aiService.IsAvailable // Needed for script gen step
                &amp;&amp; _ttsService.IsAvailable // Check TTS service
                &amp;&amp; _videoProcessorService.IsAvailable; // Check Video Processor service
        }

        // Helper to generate a simple SRT file with one entry spanning the audio duration
        private async Task GenerateSrtFileAsync(string text, TimeSpan duration, string outputPath, CancellationToken cancellationToken)
        {
            var srtContent = new StringBuilder();
            string startTime = "00:00:00,000";
            // Format duration as HH:mm:ss,fff
            string endTime = duration.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);

            srtContent.AppendLine("1"); // Sequence number
            srtContent.AppendLine($"{startTime} --> {endTime}"); // Timestamp
            srtContent.AppendLine(text.Trim()); // Subtitle text (basic, no complex formatting)
            srtContent.AppendLine(); // Blank line separator

            await File.WriteAllTextAsync(outputPath, srtContent.ToString(), Encoding.UTF8, cancellationToken);
        }
    }
}