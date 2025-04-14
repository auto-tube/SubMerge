using AutoTubeWpf.Services;
using AutoTubeWpf.ViewModels;
using Microsoft.Extensions.DependencyInjection; // Added for DI
using System;
using System.Diagnostics;
using System.Windows;

namespace AutoTubeWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider; // Store the service provider
        private ILoggerService? _logger; // Keep logger accessible for OnExit

        public App()
        {
            // Initialize DI container
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Services (Singleton lifetime seems appropriate for most)
            services.AddSingleton<ILoggerService, LoggerService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IVideoProcessorService, VideoProcessorService>();
            services.AddSingleton<IAiService, GeminiService>();
            services.AddSingleton<ITtsService, PollyTtsService>();
            services.AddSingleton<IFileOrganizerService, FileOrganizerService>();
            services.AddSingleton<IDialogService, DialogService>(); // Register DialogService

            // Register ViewModels
            services.AddSingleton<MainWindowViewModel>();
            // Tab ViewModels are created by MainWindowViewModel constructor for simplicity now

            // Register MainWindow
            services.AddTransient<MainWindow>();
        }


        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (_serviceProvider == null)
            {
                 MessageBox.Show("Critical error: Service provider not initialized.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

            // Resolve logger early for logging startup process
            _logger = _serviceProvider.GetService<ILoggerService>();
            if (_logger == null)
            {
                 // Fallback if logger itself fails to resolve
                 Debug.WriteLine("[App.OnStartup] CRITICAL ERROR: Failed to resolve ILoggerService from container.");
                 MessageBox.Show("Critical error: Could not initialize logger service.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

            _logger.LogInfo("Application starting (with DI)...");

            // --- Initialize Core Services Async ---
            // Configuration needs to be loaded before other services might use it
            var configService = _serviceProvider.GetService<IConfigurationService>();
            var videoService = _serviceProvider.GetService<IVideoProcessorService>();
            var aiService = _serviceProvider.GetService<IAiService>();
            var ttsService = _serviceProvider.GetService<ITtsService>();
            // DialogService and FileOrganizerService don't have async init currently

            if (configService == null || videoService == null || aiService == null || ttsService == null)
            {
                 _logger.LogCritical("CRITICAL ERROR: Failed to resolve one or more core services.");
                 MessageBox.Show("Critical error: Failed to initialize core application services.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

            try
            {
                await configService.LoadSettingsAsync();
                _logger.LogInfo("Configuration loaded.");

                bool ffmpegOk = await videoService.InitializeAsync();
                 if (ffmpegOk) _logger.LogInfo("VideoProcessorService initialized successfully.");
                 else _logger.LogWarning("VideoProcessorService initialized, but FFmpeg/FFprobe were not found/verified.");

                string? googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? configService.CurrentSettings.GoogleApiKey;
                aiService.Configure(googleApiKey);
                 if (aiService.IsAvailable) _logger.LogInfo("AI Service (Gemini) configured successfully.");
                 else _logger.LogWarning("AI Service (Gemini) could not be configured.");

                string? awsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? configService.CurrentSettings.AwsAccessKeyId;
                string? awsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? configService.CurrentSettings.AwsSecretAccessKey;
                string? awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? configService.CurrentSettings.AwsRegionName;
                ttsService.Configure(awsAccessKey, awsSecretKey, awsRegion);
                 if (ttsService.IsAvailable) _logger.LogInfo("TTS Service (Polly) configured successfully.");
                 else _logger.LogWarning("TTS Service (Polly) could not be configured.");

                 _logger.LogInfo("FileOrganizerService initialized (via DI).");
                 _logger.LogInfo("DialogService initialized (via DI).");

            }
            catch (Exception ex)
            {
                 _logger.LogCritical($"CRITICAL ERROR during async service initialization: {ex.Message}", ex);
                 MessageBox.Show($"A critical error occurred during application startup:\n\n{ex.Message}\n\nThe application will now exit.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }


            // --- Resolve and Show Main Window ---
            try
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                _logger.LogInfo("MainWindow resolved from container.");

                mainWindow.Show();
                _logger.LogInfo("MainWindow shown.");
            }
            catch (Exception ex)
            {
                 _logger.LogCritical($"CRITICAL ERROR resolving or showing MainWindow: {ex.Message}", ex);
                 MessageBox.Show($"A critical error occurred creating or showing the main window:\n\n{ex.Message}\n\nThe application will now exit.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _logger?.LogInfo($"Application exiting with code {e.ApplicationExitCode}.");
            // Dispose the service provider
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}