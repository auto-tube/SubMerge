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
        // private readonly Progress<VideoProcessingProgress> _progressReporter; // No longer needed here

        // --- UI Bound Properties ---
        [ObservableProperty]
        private string? _statusText = "Status: Initializing...";

        [ObservableProperty]
        private double _progressValue = 0.0;

        [ObservableProperty]
        private string? _estimatedTime = "Est. Time: N/A";

        // --- Tab ViewModels ---
        public ClippingViewModel? ClippingVM { get; } // Made nullable
        public AiShortViewModel? AiShortVM { get; }   // Made nullable
        public MetadataViewModel? MetadataVM { get; } // Made nullable
        public SettingsViewModel? SettingsVM { get; } // Made nullable

        // Constructor accepting services
        public MainWindowViewModel(
            // Core services needed by MainWindowViewModel itself
            ILoggerService loggerService,
            // Services needed by tab VMs are injected into them by DI container

            // Injected Tab ViewModels
            ClippingViewModel clippingVM, // Made non-nullable again
            AiShortViewModel aiShortVM,   // Made non-nullable again
            MetadataViewModel metadataVM, // Made non-nullable again
            SettingsViewModel settingsVM, // Made non-nullable again
            // Inject the singleton Progress<T> instance
            Progress<VideoProcessingProgress> progressReporter)
        {
            _logger = loggerService;
            _logger.LogInfo("MainWindowViewModel initializing...");

            // Subscribe to the injected Progress<T> instance's event
            // _progressReporter = new Progress<VideoProcessingProgress>(HandleProgressUpdate); // Remove instance creation
            if (progressReporter != null)
            {
                progressReporter.ProgressChanged += (sender, progress) => HandleProgressUpdate(progress);
            }

            // Assign injected ViewModels
            ClippingVM = clippingVM;
            AiShortVM = aiShortVM;
            MetadataVM = metadataVM;
            SettingsVM = settingsVM;


            // Pass the progress reporter to ViewModels that need it
            // (Assuming they have a method or property to accept it post-construction, or accept via constructor)
            // If they need it at construction, they should inject IProgress<VideoProcessingProgress> directly
            // Both ClippingViewModel and AiShortViewModel accept IProgress via constructor,
            // so the DI container handles providing the instance registered in App.xaml.cs.


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
            if (!string.IsNullOrWhiteSpace(progress.Message) &&
                (progress.Message.Contains("Processing video") || progress.Message.Contains("Combining") || progress.Message.Contains("complete") || progress.Message.Contains("Finished") || progress.Message.Contains("Cancelled") || progress.Message.Contains("Error")))
            {
                 StatusText = progress.Message;
            }
        }

        // Method to expose the progress reporter for DI factory (REMOVED)
        /*
        public IProgress<VideoProcessingProgress> GetProgressReporter()
        {
            // _progressReporter is guaranteed to be initialized by the constructor.
            return _progressReporter;
        }
        */

        // Example command (not used yet)
        [RelayCommand]
        private void DoSomething()
        {
            StatusText = "Did something!";
            _logger.LogDebug("DoSomething command executed.");
        }
    }
}