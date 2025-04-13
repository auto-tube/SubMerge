using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel; // For potential tab viewmodels later
using System.Threading.Tasks;
using AutoTubeWpf.Services; // Added for service interfaces

namespace AutoTubeWpf.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ILoggerService _logger; // Store logger

        // Example property for status bar
        [ObservableProperty]
        private string? _statusText = "Status: Initializing...";

        // Example property for progress bar
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
            ITtsService ttsService) // Added ITtsService
        {
            _logger = loggerService;
            _logger.LogInfo("MainWindowViewModel initializing...");

            // Initialize tab ViewModels here
            // Pass all required services to each ViewModel
            ClippingVM = new ClippingViewModel(loggerService, videoProcessorService, configurationService);
            // TODO: Pass other required services (VideoProcessor, Config) to AiShortViewModel later
            AiShortVM = new AiShortViewModel(loggerService, aiService, ttsService /*, other services */); // Pass ITtsService
            MetadataVM = new MetadataViewModel(loggerService, aiService);
            SettingsVM = new SettingsViewModel(configurationService, loggerService);

            StatusText = "Status: Ready."; // Update status after init
            _logger.LogInfo("MainWindowViewModel initialized.");
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