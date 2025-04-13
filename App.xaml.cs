using AutoTubeWpf.Services;
using AutoTubeWpf.ViewModels;
using System;
using System.Diagnostics; // Keep for fallback logging if logger fails
using System.Windows;

namespace AutoTubeWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IConfigurationService? _configurationService;
        private ILoggerService? _loggerService;
        private IVideoProcessorService? _videoProcessorService;
        private IAiService? _aiService;
        private ITtsService? _ttsService; // Added TTS service field
        private MainWindowViewModel? _mainWindowViewModel;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize Logger First (crucial for logging subsequent errors)
            try
            {
                _loggerService = new LoggerService();
            }
            catch (Exception loggerEx)
            {
                 // Fallback to Debug/Console if logger fails
                 Debug.WriteLine($"[App.OnStartup] CRITICAL ERROR initializing LoggerService: {loggerEx}");
                 Console.WriteLine($"[App.OnStartup] CRITICAL ERROR initializing LoggerService: {loggerEx}");
                 MessageBox.Show($"A critical error occurred initializing the application logger:\n\n{loggerEx.Message}\n\nThe application will now exit.",
                                 "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

            _loggerService.LogInfo("Application starting...");


            // --- Service Initialization ---
            try
            {
                // Configuration Service
                _configurationService = new ConfigurationService();
                await _configurationService.LoadSettingsAsync();
                _loggerService.LogInfo("Configuration loaded.");

                // Video Processor Service (depends on Logger)
                _videoProcessorService = new VideoProcessorService(_loggerService);
                bool ffmpegOk = await _videoProcessorService.InitializeAsync();
                if (ffmpegOk)
                {
                    _loggerService.LogInfo("VideoProcessorService initialized successfully (FFmpeg/FFprobe found).");
                }
                else
                {
                     _loggerService.LogWarning("VideoProcessorService initialized, but FFmpeg/FFprobe were not found or verified. Video processing features will be disabled.");
                }

                // AI Service (depends on Logger and Configuration)
                _aiService = new GeminiService(_loggerService);
                string? googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                                    ?? _configurationService.CurrentSettings.GoogleApiKey;
                _aiService.Configure(googleApiKey);
                if (_aiService.IsAvailable)
                {
                     _loggerService.LogInfo("AI Service (Gemini) configured successfully.");
                }
                else
                {
                     _loggerService.LogWarning("AI Service (Gemini) could not be configured. AI features may be unavailable. Check API key/credentials and logs.");
                }

                // TTS Service (depends on Logger and Configuration)
                _ttsService = new PollyTtsService(_loggerService);
                // Prioritize environment variables for AWS credentials
                string? awsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
                                    ?? _configurationService.CurrentSettings.AwsAccessKeyId;
                string? awsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
                                    ?? _configurationService.CurrentSettings.AwsSecretAccessKey;
                string? awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") // Or AWS_DEFAULT_REGION
                                    ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
                                    ?? _configurationService.CurrentSettings.AwsRegionName;
                _ttsService.Configure(awsAccessKey, awsSecretKey, awsRegion);
                 if (_ttsService.IsAvailable)
                {
                     _loggerService.LogInfo("TTS Service (Polly) configured successfully.");
                     // Optionally fetch voices here if needed globally, or let ViewModel do it on demand
                     // await _ttsService.GetAvailableVoicesAsync();
                }
                else
                {
                     _loggerService.LogWarning("TTS Service (Polly) could not be configured. TTS features may be unavailable. Check AWS credentials/region and logs.");
                }

            }
            catch (Exception ex)
            {
                // Log critical failure during essential service initialization
                _loggerService?.LogCritical($"CRITICAL ERROR initializing services: {ex.Message}", ex); // Use logger if available
                MessageBox.Show($"A critical error occurred during application startup:\n\n{ex.Message}\n\nThe application will now exit.",
                                "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Ensure clean shutdown if possible
                Shutdown(-1); // Exit with an error code
                return; // Stop further execution
            }


            // --- ViewModel Initialization ---
            try
            {
                // Ensure core services are initialized before passing them
                // Note: AI/TTS services might not be available, but we still proceed
                if (_loggerService == null || _configurationService == null || _videoProcessorService == null || _aiService == null || _ttsService == null)
                {
                    throw new InvalidOperationException("Core services failed to initialize.");
                }

                // Pass necessary services/settings to the ViewModel constructor
                _mainWindowViewModel = new MainWindowViewModel(_loggerService, _configurationService, _videoProcessorService, _aiService, _ttsService); // Pass TTS service
                _loggerService.LogInfo("MainWindowViewModel created.");
            }
             catch (Exception ex)
            {
                _loggerService?.LogCritical($"CRITICAL ERROR creating MainWindowViewModel: {ex.Message}", ex);
                MessageBox.Show($"A critical error occurred creating the main view model:\n\n{ex.Message}\n\nThe application will now exit.",
                                "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            // --- Window Creation and DataContext Assignment ---
             try
            {
                var mainWindow = new MainWindow
                {
                    DataContext = _mainWindowViewModel // Set the DataContext
                };
                _loggerService.LogInfo("MainWindow created and DataContext set.");

                mainWindow.Show();
                _loggerService.LogInfo("MainWindow shown.");
            }
            catch (Exception ex)
            {
                 _loggerService?.LogCritical($"CRITICAL ERROR creating or showing MainWindow: {ex.Message}", ex);
                 MessageBox.Show($"A critical error occurred creating or showing the main window:\n\n{ex.Message}\n\nThe application will now exit.",
                                 "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _loggerService?.LogInfo($"Application exiting with code {e.ApplicationExitCode}.");
            // Perform any final cleanup here if needed
            base.OnExit(e);
        }
    }
}