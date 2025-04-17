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
// using System.Windows; // No longer needed

namespace AutoTubeWpf.ViewModels
{
    public partial class AiShortViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly IAiService _aiService;
        private readonly ITtsService _ttsService;
        private readonly IVideoProcessorService _videoProcessorService;
        private readonly IConfigurationService _configurationService;
        private readonly IFileOrganizerService _fileOrganizerService;
        private readonly IDialogService _dialogService;
        private readonly ISubtitleService _subtitleService; // Added Subtitle service
        private readonly IProgress<VideoProcessingProgress>? _progressReporter;
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
        private int _fontSize = 48;

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
            IVideoProcessorService videoProcessorService,
            IConfigurationService configurationService,
            IFileOrganizerService fileOrganizerService,
            IDialogService dialogService,
            ISubtitleService subtitleService, // Added Subtitle service
            IProgress<VideoProcessingProgress>? progressReporter = null)
       {
           _logger = loggerService;
           _aiService = aiService;
           _ttsService = ttsService;
           _videoProcessorService = videoProcessorService;
           _configurationService = configurationService;
           _fileOrganizerService = fileOrganizerService;
           _dialogService = dialogService;
           _subtitleService = subtitleService; // Store Subtitle service
           _progressReporter = progressReporter;

            _logger.LogInfo("AiShortViewModel initialized.");
            if (string.IsNullOrWhiteSpace(OutputFolderPath))
            {
                 OutputFolderPath = _configurationService.CurrentSettings.DefaultOutputPath;
                 _logger.LogDebug($"Default AI Short output folder set from config: {OutputFolderPath}");
            }
            // Call LoadVoicesAsync safely, catching potential synchronous exceptions
            try
            {
                // Don't await here in constructor, but catch immediate errors
                _ = LoadVoicesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initiating LoadVoicesAsync in AiShortViewModel constructor: {ex.Message}", ex);
                // Handle error appropriately, maybe set a flag or default state
            }
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
                if (voices != null && voices.Any())
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
             if (!string.IsNullOrWhiteSpace(OutputFolderPath) && Directory.Exists(OutputFolderPath))
            {
                dialog.SelectedPath = OutputFolderPath;
            }
             else
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
                 _dialogService.ShowWarningDialog("Please enter a prompt for script generation.", "Input Required");
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
                    _dialogService.ShowInfoDialog("Script generated successfully and placed in the text box.", "Script Generated");
                }
                else
                {
                    ScriptText = "Script generation failed or returned empty.";
                    _logger.LogWarning("Script generation failed or returned empty.");
                     _dialogService.ShowWarningDialog("Script generation failed or returned an empty result. Check logs for details.", "Generation Failed");
                }
            }
            catch (OperationCanceledException)
            {
                ScriptText = "Script generation cancelled.";
                _logger.LogInfo("Script generation was cancelled by the user.");
                 _dialogService.ShowInfoDialog("Script generation was cancelled.", "Cancelled");
            }
            catch (InvalidOperationException ioex)
            {
                 ScriptText = $"Script generation failed: {ioex.Message}";
                 _logger.LogError($"Script generation failed: {ioex.Message}", ioex);
                 _dialogService.ShowErrorDialog($"Script generation failed:\n{ioex.Message}\n\nPlease ensure the AI Service is configured correctly in Settings.", "Service Error");
            }
            catch (Exception ex)
            {
                ScriptText = $"Script generation failed: {ex.Message}";
                _logger.LogError("An unexpected error occurred during script generation.", ex);
                _dialogService.ShowErrorDialog($"An unexpected error occurred during script generation:\n{ex.Message}", "Error");
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
            return !IsGeneratingScript && !IsGeneratingShort && !string.IsNullOrWhiteSpace(ScriptPrompt) && _aiService.IsAvailable;
        }


        [RelayCommand(CanExecute = nameof(CanGenerateAiShort))]
        private async Task GenerateAiShortAsync()
        {
            // --- ADDED Logging ---
            _logger.LogInfo("--- GenerateAiShortAsync method entered ---");
            // --- END ADDED ---

            // Relying on CanExecute check

            _shortGenerationCts = new CancellationTokenSource();
            IsGeneratingShort = true;
            GenerateAiShortCommand.NotifyCanExecuteChanged(); // Still notify to potentially disable during run

            string tempAudioFilePath = Path.Combine(Path.GetTempPath(), $"autotube_tts_{Guid.NewGuid()}.mp3");
            string tempSrtFilePath = Path.Combine(Path.GetTempPath(), $"autotube_sub_{Guid.NewGuid()}.srt");
            string initialOutputPath = Path.Combine(OutputFolderPath!, $"AI_Short_{Path.GetFileNameWithoutExtension(BackgroundVideoPath)}_{DateTime.Now:yyyyMMddHHmmss}.mp4");
            string finalOrganizedPath = initialOutputPath;
            SynthesisResult? ttsResult = null;

            try
            {
                // 1. Synthesize Speech (including speech marks)
                _logger.LogDebug($"Synthesizing speech to temp file: {tempAudioFilePath}");
                _progressReporter?.Report(new VideoProcessingProgress { Percentage = 0, Message = "Synthesizing speech..." });
                ttsResult = await _ttsService.SynthesizeSpeechAsync(ScriptText!, SelectedPollyVoice!, tempAudioFilePath, true, _shortGenerationCts.Token); // Request speech marks
                if (!ttsResult.Success || string.IsNullOrEmpty(ttsResult.OutputFilePath)) throw new Exception($"TTS synthesis failed: {ttsResult.ErrorMessage ?? "Unknown error"}");
                _logger.LogInfo("TTS synthesis successful.");

                // 2. Get Audio Duration
                _progressReporter?.Report(new VideoProcessingProgress { Percentage = 0, Message = "Getting audio duration..." });
                TimeSpan? audioDuration = await _videoProcessorService.GetVideoDurationAsync(ttsResult.OutputFilePath, _shortGenerationCts.Token);
                if (audioDuration == null) throw new Exception("Failed to get duration of generated audio file.");
                _logger.LogInfo($"Generated audio duration: {audioDuration.Value}");

                // 3. Generate Subtitle File using SubtitleService and Speech Marks
                _progressReporter?.Report(new VideoProcessingProgress { Percentage = 0, Message = "Generating subtitles..." });
                _logger.LogDebug($"Generating temporary SRT file: {tempSrtFilePath}");
                // Pass speech marks to subtitle service if available
                await _subtitleService.GenerateSrtFileAsync(ScriptText!, audioDuration.Value, tempSrtFilePath, speechMarks: ttsResult.SpeechMarks, cancellationToken: _shortGenerationCts.Token); // Pass marks
                _logger.LogInfo("Temporary SRT file generated.");

                // 4. Combine Video, Audio, Subtitles
                _logger.LogDebug($"Combining inputs for output: {initialOutputPath}");
                await _videoProcessorService.CombineVideoAudioSubtitlesAsync(
                     BackgroundVideoPath!,
                     ttsResult.OutputFilePath, // Use path from result
                     tempSrtFilePath,
                     initialOutputPath,
                     _progressReporter,
                     _shortGenerationCts.Token);
               _logger.LogInfo("Video combination successful.");
                finalOrganizedPath = initialOutputPath;

                // 5. Organize Output (if enabled)
                bool organizeOutput = _configurationService.CurrentSettings.OrganizeOutput;
                if (organizeOutput)
                {
                    _logger.LogDebug($"Organizing output file: {initialOutputPath}");
                    _progressReporter?.Report(new VideoProcessingProgress { Percentage = 1.0, Message = "Organizing output..." });
                    try
                    {
                        finalOrganizedPath = await _fileOrganizerService.OrganizeOutputFileAsync(initialOutputPath, BackgroundVideoPath!, OutputFolderPath!, "ai_short");
                        _logger.LogInfo($"AI Short organized to: {finalOrganizedPath}");
                    }
                    catch (Exception orgEx)
                    {
                         _logger.LogError($"Failed to organize AI Short. File remains at '{initialOutputPath}'. Error: {orgEx.Message}", orgEx);
                         finalOrganizedPath = initialOutputPath;
                         _dialogService.ShowWarningDialog($"AI Short generated successfully but failed to organize the output file.\nFile saved at: {finalOrganizedPath}\nError: {orgEx.Message}", "Organization Warning");
                         goto SkipSuccessMessage;
                    }
                }

                _logger.LogInfo("AI Short generation finished successfully.");
                 _progressReporter?.Report(new VideoProcessingProgress { Percentage = 1.0, Message = "AI Short generation complete." });
                _dialogService.ShowInfoDialog($"AI Short Generation Complete!\nOutput: {finalOrganizedPath}", "Complete");

                SkipSuccessMessage:;

            }
            catch (OperationCanceledException)
            {
                 _logger.LogInfo("AI Short generation was cancelled.");
                 _progressReporter?.Report(new VideoProcessingProgress { Percentage = 0, Message = "Cancelled." });
                 _dialogService.ShowInfoDialog("AI Short generation was cancelled.", "Cancelled");
            }
            catch (InvalidOperationException ioex)
            {
                 _logger.LogError($"AI Short generation failed due to service issue: {ioex.Message}", ioex);
                 _progressReporter?.Report(new VideoProcessingProgress { Percentage = 0, Message = $"Error: {ioex.Message}" });
                 _dialogService.ShowErrorDialog($"AI Short generation failed:\n{ioex.Message}\n\nPlease ensure required services are configured correctly.", "Service Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred during AI Short generation: {ex.Message}", ex);
                 _progressReporter?.Report(new VideoProcessingProgress { Percentage = 0, Message = $"Error: {ex.Message}" });
                _dialogService.ShowErrorDialog($"An unexpected error occurred during AI Short generation:\n{ex.Message}", "Error");
            }
            finally
            {
                // Clean up temporary files
                if (ttsResult?.OutputFilePath != null) try { if (File.Exists(ttsResult.OutputFilePath)) File.Delete(ttsResult.OutputFilePath); } catch (Exception ex) { _logger.LogWarning($"Failed to delete temp audio file '{ttsResult.OutputFilePath}': {ex.Message}"); }
                try { if (File.Exists(tempSrtFilePath)) File.Delete(tempSrtFilePath); } catch (Exception ex) { _logger.LogWarning($"Failed to delete temp SRT file '{tempSrtFilePath}': {ex.Message}"); }

                IsGeneratingShort = false;
                _shortGenerationCts?.Dispose();
                _shortGenerationCts = null;
                GenerateAiShortCommand.NotifyCanExecuteChanged();
            }
        }
        // CanGenerateAiShort method still exists and includes logging
        private bool CanGenerateAiShort()
        {
            // Log entry point
            _logger.LogDebug($"Checking CanGenerateAiShort...");

            bool bgVideoOk = !string.IsNullOrWhiteSpace(BackgroundVideoPath) && File.Exists(BackgroundVideoPath);
            bool outputFolderOk = !string.IsNullOrWhiteSpace(OutputFolderPath) && Directory.Exists(OutputFolderPath);
            bool scriptOk = !string.IsNullOrWhiteSpace(ScriptText);
            bool voiceOk = !string.IsNullOrWhiteSpace(SelectedPollyVoice);
            bool aiOk = _aiService.IsAvailable;
            bool ttsOk = _ttsService.IsAvailable;
            bool videoOk = _videoProcessorService.IsAvailable;
            bool notBusy = !IsGeneratingScript && !IsGeneratingShort;

            bool canGenerate = notBusy && bgVideoOk && outputFolderOk && scriptOk && voiceOk && aiOk && ttsOk && videoOk;

            // Log the state of each check
            _logger.LogDebug($"CanGenerateAiShort Result: NotBusy={notBusy}, BgVideoOk={bgVideoOk} ('{BackgroundVideoPath}'), OutputFolderOk={outputFolderOk} ('{OutputFolderPath}'), ScriptOk={scriptOk}, VoiceOk={voiceOk} ('{SelectedPollyVoice}'), AiOk={aiOk}, TtsOk={ttsOk}, VideoOk={videoOk} ==> CanGenerate={canGenerate}");

            return canGenerate;
        }

        // --- ADDED: Public helper method for logging status ---
        public void LogCanGenerateAiShortStatus()
        {
             // This just calls the existing private method to log the details
             _logger.LogDebug("LogCanGenerateAiShortStatus called from code-behind."); // Add specific marker
             CanGenerateAiShort();
        }
        // --- END ADDED ---

        // Removed local GenerateSrtFileAsync helper method - now using ISubtitleService
    }
}