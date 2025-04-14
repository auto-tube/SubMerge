using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System; // For IProgress
using System.Collections.ObjectModel; // For potential tab viewmodels later
using System.Threading.Tasks;
using AutoTubeWpf.Services; // Added for service interfaces

namespace AutoTubeWpf.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly Progress<VideoProcessingProgress> _progressReporter;

        // --- UI Bound Properties ---
        [ObservableProperty]
        private string? _statusText = "Status: Initializing...";

        [ObservableProperty]
        private double _progressValue = 0.0;

        [ObservableProperty]
        private string? _estimatedTime = "Est. Time: N/A";

        // --- Tab ViewModels ---
        public ClippingViewModel ClippingVM { get; }
        public AiShortViewModel AiShortVM { get; }
        public MetadataViewModel MetadataVM { get; }
        public SettingsViewModel SettingsVM { get; }

        // Constructor accepting services
        public MainWindowViewModel(
            ILoggerService loggerService,
            IConfigurationService configurationService,
            IVideoProcessorService videoProcessorService,
            IAiService aiService,
            ITtsService ttsService,
            IFileOrganizerService fileOrganizerService,
            IDialogService dialogService) // Added IDialogService
        {
            _logger = loggerService;
            _logger.LogInfo("MainWindowViewModel initializing...");

            // Initialize Progress Reporter
            _progressReporter = new Progress<VideoProcessingProgress>(HandleProgressUpdate);

            // Initialize tab ViewModels here
            // Pass all required services including DialogService and ProgressReporter
            ClippingVM = new ClippingViewModel(loggerService, videoProcessorService, configurationService, fileOrganizerService, dialogService, _progressReporter); // Pass DialogService & Progress
            AiShortVM = new AiShortViewModel(loggerService, aiService, ttsService, videoProcessorService, configurationService, fileOrganizerService, dialogService, _progressReporter); // Pass DialogService & Progress
            MetadataVM = new MetadataViewModel(loggerService, aiService, dialogService); // Pass DialogService
            SettingsVM = new SettingsViewModel(configurationService, loggerService, dialogService); // Pass DialogService

            StatusText = "Status: Ready."; // Update status after init
            _logger.LogInfo("MainWindowViewModel initialized.");
        }

        // --- Progress Handling ---
        private void HandleProgressUpdate(VideoProcessingProgress progress)
        {
            ProgressValue = progress.Percentage;

            if (progress.EstimatedRemaining.HasValue)
            {
                EstimatedTime = $"Est. Time: {progress.EstimatedRemaining.Value:mm\\:ss}";
            }
            else if (progress.Percentage >= 1.0)
            {
                 EstimatedTime = "Est. Time: Done";
            }
            else if (ProgressValue > 0)
            {
                 EstimatedTime = "Est. Time: Calculating...";
            }
            else
            {
                 EstimatedTime = "Est. Time: N/A";
            }

            // Update status text only if the message seems relevant for overall status
            if (!string.IsNullOrWhiteSpace(progress.Message) &amp;&amp;
                (progress.Message.Contains("Processing video") || progress.Message.Contains("Combining") || progress.Message.Contains("complete") || progress.Message.Contains("Finished") || progress.Message.Contains("Cancelled") || progress.Message.Contains("Error")))
            {
                 StatusText = progress.Message;
            }
        }


        // Example command (not used yet)
        [RelayCommand]
        private void DoSomething()
        {
            StatusText = "Did something!";
            _logger.LogDebug("DoSomething command executed.");
        }
    }
}