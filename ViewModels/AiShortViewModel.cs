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
using System.Diagnostics; // Added for Debug.WriteLine

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
        private List<string> _availableSubtitleAlignments = new List<string> { "Bottom Center", "Bottom Left", "Bottom Right" }; // Added
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))] // Added Notify
        private string? _selectedSubtitleAlignment = "Bottom Center"; // Added, default to center
 
        // --- Added Subtitle Styling Options ---
        [ObservableProperty]
        private List<string> _availableSubtitleFonts = new List<string> { "Arial", "Impact", "Verdana", "Tahoma", "Comic Sans MS", "Segoe UI" }; // Added Font List
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private string _subtitleFontName = "Arial"; // Default remains
 
        // Use System.Windows.Media.Color for binding to ColorPicker
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private System.Windows.Media.Color _subtitleFontColor = System.Windows.Media.Colors.White;
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private System.Windows.Media.Color _subtitleOutlineColor = System.Windows.Media.Colors.Black;
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private int _subtitleOutlineThickness = 2;
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private bool _useSubtitleBackgroundBox = false;
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private System.Windows.Media.Color _subtitleBackgroundColor = System.Windows.Media.Color.FromArgb(128, 0, 0, 0); // Semi-transparent black
 
        // --- Added Background Effects ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private bool _applyBackgroundBlur = false;
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAiShortCommand))]
        private bool _applyBackgroundGrayscale = false;
        // --- End Added ---
 
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateScriptCommand))]
        private string? _scriptPrompt;

        partial void OnScriptPromptChanged(string? value)
        {
            GenerateScriptCommand.NotifyCanExecuteChanged();
            GenerateAiShortCommand.NotifyCanExecuteChanged();
        }

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

            // Explicitly initialize flags to false
            _isGeneratingScript = false;
            _isGeneratingShort = false;

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
            _logger.LogInfo("[ViewModel] GenerateScriptAsync command entered."); 

            if (string.IsNullOrWhiteSpace(ScriptPrompt))
            {
                 _logger.LogWarning("[ViewModel] ScriptPrompt is null or whitespace. Aborting.");
                 _dialogService.ShowWarningDialog("Please enter a prompt for script generation.", "Input Required");
                 return;
            }

            _logger.LogInfo("GenerateScript command executed."); // Original log
            _scriptGenerationCts = new CancellationTokenSource();
            IsGeneratingScript = true;
            GenerateScriptCommand.NotifyCanExecuteChanged();
            GenerateAiShortCommand.NotifyCanExecuteChanged(); // Also update other command

            try
            {
                ScriptText = "Generating script...";
                _logger.LogInfo("[ViewModel] Calling _aiService.GenerateScriptAsync..."); 
                string? generatedScript = await _aiService.GenerateScriptAsync(ScriptPrompt, _scriptGenerationCts.Token);
                _logger.LogInfo("[ViewModel] _aiService.GenerateScriptAsync returned."); 

                if (generatedScript != null)
                {
                    ScriptText = generatedScript;
                    _logger.LogInfo("Script generated successfully.");
                    _dialogService.ShowInfoDialog("Script generated successfully and placed in the text box.", "Script Generated");
                }
                else
                {
                    _logger.LogWarning("[ViewModel] Script generation returned null or empty from service. Attempting to show warning dialog."); 
                    ScriptText = "Script generation failed or returned empty.";
                    _logger.LogWarning("Script generation failed or returned empty."); // Original log
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
                _logger.LogError($"An unexpected error occurred during script generation.", ex);
                _dialogService.ShowErrorDialog($"An unexpected error occurred during script generation:\n{ex.Message}", "Error");
            }
            finally
            {
                _logger.LogInfo("[ViewModel] Entering finally block for GenerateScriptAsync."); 
                IsGeneratingScript = false;
                _scriptGenerationCts?.Dispose();
                _scriptGenerationCts = null;
                GenerateScriptCommand.NotifyCanExecuteChanged();
                GenerateAiShortCommand.NotifyCanExecuteChanged(); // Also update other command
            }
        }
        private bool CanGenerateScript()
        {
            // Use Debug.WriteLine as a fallback if _logger is null or not working
            try 
            {
                 bool isAvailable = _aiService?.IsAvailable ?? false; 
                 bool hasPrompt = !string.IsNullOrWhiteSpace(ScriptPrompt);
                 bool notBusy = !IsGeneratingScript && !IsGeneratingShort;
                 bool result = notBusy && hasPrompt && isAvailable;

                 string logMessage = $"[Debug] Checking CanGenerateScript: IsGeneratingScript={IsGeneratingScript}, IsGeneratingShort={IsGeneratingShort}, HasPrompt={hasPrompt}, IsAiAvailable={isAvailable} ==> Result={result}";
                 Debug.WriteLine(logMessage); // Write to VS Output window

                 // Also try the main logger just in case
                 if (_logger != null) 
                 {
                    _logger.LogDebug($"Checking CanGenerateScript: IsGeneratingScript={IsGeneratingScript}, IsGeneratingShort={IsGeneratingShort}, ScriptPrompt='{ScriptPrompt}', IsAiAvailable={isAvailable}");
                 } 
                 else 
                 {
                    Debug.WriteLine("[Debug] _logger is NULL in CanGenerateScript");
                 }

                 return result;
            } 
            catch (Exception ex) 
            {
                 Debug.WriteLine($"[Debug] EXCEPTION in CanGenerateScript: {ex.Message}");
                 return false; // Prevent execution if check fails
            }
        }


        [RelayCommand(CanExecute = nameof(CanGenerateAiShort))]
        private async Task GenerateAiShortAsync()
        {
            _logger.LogInfo("--- GenerateAiShortAsync method entered ---");

            // Relying on CanExecute check

            _shortGenerationCts = new CancellationTokenSource();
            IsGeneratingShort = true;
            GenerateAiShortCommand.NotifyCanExecuteChanged(); 
            GenerateScriptCommand.NotifyCanExecuteChanged(); // Also update other command

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
                _logger.LogDebug($"Combining inputs for output: {initialOutputPath} with alignment: {SelectedSubtitleAlignment}, Font: {SubtitleFontName}, Color: {SubtitleFontColor}"); // Added more logging
                await _videoProcessorService.CombineVideoAudioSubtitlesAsync(
                     backgroundVideoPath: BackgroundVideoPath!,
                     audioPath: ttsResult.OutputFilePath, // Use path from result
                     subtitlePath: tempSrtFilePath,
                     outputPath: initialOutputPath,
                     subtitleAlignment: SelectedSubtitleAlignment!,
                     subtitleFontName: SubtitleFontName,
                     subtitleFontColor: SubtitleFontColor,
                     subtitleOutlineColor: SubtitleOutlineColor,
                     subtitleOutlineThickness: SubtitleOutlineThickness,
                     useSubtitleBackgroundBox: UseSubtitleBackgroundBox,
                     subtitleBackgroundColor: SubtitleBackgroundColor,
                     applyBackgroundBlur: ApplyBackgroundBlur,           // Pass Blur flag
                     applyBackgroundGrayscale: ApplyBackgroundGrayscale, // Pass Grayscale flag
                     progress: _progressReporter,
                     cancellationToken: _shortGenerationCts.Token);
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
                 _logger.LogInfo("[ViewModel] Entering finally block for GenerateAiShortAsync."); // ADDED LOGGING HERE
                // Clean up temporary files
                if (ttsResult?.OutputFilePath != null) try { if (File.Exists(ttsResult.OutputFilePath)) File.Delete(ttsResult.OutputFilePath); } catch (Exception ex) { _logger.LogWarning($"Failed to delete temp audio file '{ttsResult.OutputFilePath}': {ex.Message}"); }
                try { if (File.Exists(tempSrtFilePath)) File.Delete(tempSrtFilePath); } catch (Exception ex) { _logger.LogWarning($"Failed to delete temp SRT file '{tempSrtFilePath}': {ex.Message}"); }

                IsGeneratingShort = false;
                _shortGenerationCts?.Dispose();
                _shortGenerationCts = null;
                GenerateAiShortCommand.NotifyCanExecuteChanged();
                GenerateScriptCommand.NotifyCanExecuteChanged(); // Also update other command
            }
        }
        
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
            bool alignmentOk = !string.IsNullOrWhiteSpace(SelectedSubtitleAlignment);
            bool fontOk = !string.IsNullOrWhiteSpace(SubtitleFontName);
            // Color objects are value types, no need for null checks
            bool fontColorOk = true; // Assume Color object is always valid
            bool outlineColorOk = true;
            bool backgroundColorOk = true;
            // Blur/Grayscale flags don't affect CanExecute logic directly
            bool notBusy = !IsGeneratingScript && !IsGeneratingShort;
 
            bool canGenerate = notBusy && bgVideoOk && outputFolderOk && scriptOk && voiceOk && alignmentOk && fontOk && fontColorOk && outlineColorOk && backgroundColorOk && aiOk && ttsOk && videoOk;
 
            // Log the state of each check - Using Color.ToString() for logging
            _logger.LogDebug($"CanGenerateAiShort Result: NotBusy={notBusy}, BgVideoOk={bgVideoOk}, OutputFolderOk={outputFolderOk}, ScriptOk={scriptOk}, VoiceOk={voiceOk}, AlignmentOk={alignmentOk}, FontOk={fontOk}, FontColorOk={SubtitleFontColor}, OutlineColorOk={SubtitleOutlineColor}, BgColorOk={SubtitleBackgroundColor}, Blur={ApplyBackgroundBlur}, Grayscale={ApplyBackgroundGrayscale}, AiOk={aiOk}, TtsOk={ttsOk}, VideoOk={videoOk} ==> CanGenerate={canGenerate}"); // Added effect logging

            return canGenerate;
        }

        // --- ADDED: Public helper method for logging status ---
        // This method was missing, causing the build error in Views/AiShortView.xaml.cs
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