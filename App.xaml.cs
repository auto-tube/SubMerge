using AutoTubeWpf.Services;
using AutoTubeWpf.ViewModels;
using Microsoft.Extensions.DependencyInjection; // Added for DI
using System;
using System.Diagnostics;
using System.Windows;
using System.Threading.Tasks; // Added for async Task

namespace AutoTubeWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // --- MODIFIED: Made public for access from views ---
        public ServiceProvider? _serviceProvider; // Store the service provider
        // --- END MODIFIED ---
        private ILoggerService? _logger; // Keep logger accessible for OnExit

        public App()
        {
            // Add top-level try-catch here as well
            try
            {
                // Initialize DI container
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                _serviceProvider = serviceCollection.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                 // Use Debug.WriteLine as logger isn't available yet
                 Debug.WriteLine($"[App.Constructor] FATAL UNHANDLED EXCEPTION: {ex}");
                 // Attempt to show a message box as a last resort
                 try
                 {
                     MessageBox.Show($"A fatal unexpected error occurred during application construction:\n\n{ex.Message}\n\n{ex.StackTrace}",
                                     "Fatal Constructor Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 }
                 catch { /* Ignore if MessageBox fails */ }
                 // Cannot reliably call Shutdown() here, process might terminate anyway
                 throw; // Re-throw to ensure process terminates if possible
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Services (Minimal for testing)
            services.AddSingleton<ILoggerService, LoggerService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IVideoProcessorService, VideoProcessorService>();
            services.AddSingleton<IAiService, GeminiService>();
            services.AddSingleton<ITtsService, PollyTtsService>();
            services.AddSingleton<IFileOrganizerService, FileOrganizerService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<ISubtitleService, SubtitleService>();

            // Register ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<ClippingViewModel>();
            services.AddSingleton<AiShortViewModel>();
            services.AddSingleton<MetadataViewModel>();
            services.AddSingleton<SettingsViewModel>();

            // Register Progress<T> itself as a singleton.
            // MainWindowViewModel will subscribe to its event.
            services.AddSingleton<Progress<VideoProcessingProgress>>();
            // Register the IProgress<T> interface to resolve to the singleton Progress<T> instance.
            services.AddSingleton<IProgress<VideoProcessingProgress>>(sp => sp.GetRequiredService<Progress<VideoProcessingProgress>>());

            // Register MainWindow
            services.AddTransient<MainWindow>();
        }


        protected override async void OnStartup(StartupEventArgs e)
        {
            // Top-level try-catch to capture any startup exception
            try
            {
                // --- DEBUG LOGGING START ---
                try { System.Diagnostics.Debug.WriteLine("[App.OnStartup] Entered OnStartup try block."); } catch { }
                // --- DEBUG LOGGING END ---

                base.OnStartup(e);

                if (_serviceProvider == null)
            {
                 MessageBox.Show("Critical error: Service provider not initialized.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

            // Resolve logger early for logging startup process
            try
            {
                _logger = _serviceProvider.GetService<ILoggerService>();
            }
            catch (Exception ex)
            {
                 // Log directly to Debug output if logger resolution fails
                 Debug.WriteLine($"[App.OnStartup] CRITICAL ERROR resolving ILoggerService: {ex.Message}");
                 MessageBox.Show($"Critical error: Could not resolve logger service.\n{ex.Message}", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

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
            // DialogService, FileOrganizerService, SubtitleService don't have async init currently

            if (configService == null || videoService == null || aiService == null || ttsService == null)
            {
                 _logger.LogCritical("CRITICAL ERROR: Failed to resolve one or more core services.");
                 MessageBox.Show("Critical error: Failed to initialize core application services.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }

            try
            {
                _logger.LogDebug("Loading configuration...");
                await configService.LoadSettingsAsync();
                _logger.LogInfo("Configuration loaded.");

                _logger.LogDebug("Initializing VideoProcessorService...");
                bool ffmpegOk = await videoService.InitializeAsync();
                 if (ffmpegOk) _logger.LogInfo("VideoProcessorService initialized successfully.");
                 else _logger.LogWarning("VideoProcessorService initialized, but FFmpeg/FFprobe were not found/verified.");

                _logger.LogDebug("Configuring AI Service (Gemini)...");
                string? googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? configService.CurrentSettings.GoogleApiKey;
                aiService.Configure(googleApiKey);
                 if (aiService.IsAvailable) _logger.LogInfo("AI Service (Gemini) configured successfully.");
                 else _logger.LogWarning("AI Service (Gemini) could not be configured.");

                _logger.LogDebug("Configuring TTS Service (Polly)...");
                string? awsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? configService.CurrentSettings.AwsAccessKeyId;
                string? awsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? configService.CurrentSettings.AwsSecretAccessKey;
                string? awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? configService.CurrentSettings.AwsRegionName;
                ttsService.Configure(awsAccessKey, awsSecretKey, awsRegion);
                 if (ttsService.IsAvailable) _logger.LogInfo("TTS Service (Polly) configured successfully.");
                 else _logger.LogWarning("TTS Service (Polly) could not be configured.");

                 _logger.LogInfo("FileOrganizerService resolved (via DI).");
                 _logger.LogInfo("DialogService resolved (via DI).");
                 _logger.LogInfo("SubtitleService resolved (via DI).");
                 _logger.LogDebug("Async service initialization complete.");
            }
            catch (Exception ex)
            {
                 _logger.LogCritical($"CRITICAL ERROR during async service initialization: {ex.Message}", ex);
                 MessageBox.Show($"A critical error occurred during application startup:\n\n{ex.Message}\n\nThe application will now exit.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }


            // --- Resolve and Show Main Window ---
            // Temporarily bypassing MainWindow/DI to test basic window creation
            // _logger.LogWarning("Window showing is completely disabled for debugging."); // Removed this line
            
            _logger.LogInfo("Attempting to resolve and show MainWindow...");
            try
            {
                 // Resolve the main window using the service provider
                 System.Diagnostics.Debug.WriteLine("[App.OnStartup] Attempting _serviceProvider.GetRequiredService<MainWindow>()..."); // DEBUG
                 var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                 System.Diagnostics.Debug.WriteLine("[App.OnStartup] MainWindow instance resolved."); // DEBUG
                 _logger.LogDebug("MainWindow resolved.");

                 // Resolve the main view model (optional, if needed directly here, but usually handled by MainWindow's constructor/DataContext)
                 // var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                 // mainWindow.DataContext = mainWindowViewModel; // Set DataContext is done in MainWindow constructor now

                 System.Diagnostics.Debug.WriteLine("[App.OnStartup] Attempting mainWindow.Show()..."); // DEBUG
                 mainWindow.Show();
                 System.Diagnostics.Debug.WriteLine("[App.OnStartup] mainWindow.Show() completed."); // DEBUG
                 _logger.LogInfo("MainWindow shown successfully.");
            }
            catch (Exception ex)
            {
                 _logger.LogCritical($"CRITICAL ERROR resolving or showing MainWindow: {ex.Message}", ex);
                 MessageBox.Show($"A critical error occurred showing the main application window:\n\n{ex.Message}\n\nThe application will now exit.", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 Shutdown(-1);
                 return;
            }
            
        } // End of main try block
        catch (Exception ex) // Catch-all for any exception during OnStartup
        {
                // Use Debug.WriteLine as logger might not be available
                Debug.WriteLine($"[App.OnStartup] FATAL UNHANDLED EXCEPTION in OnStartup: {ex}"); // Added context
                // --- DEBUG LOGGING START ---
                try { System.Diagnostics.Debug.WriteLine($"[App.OnStartup] Caught final exception: {ex.GetType().Name} - {ex.Message}"); } catch { }
                // --- DEBUG LOGGING END ---
                // Attempt to show a message box as a last resort
                try
                {
                    MessageBox.Show($"A fatal unexpected error occurred during application startup:\n\n{ex.Message}\n\n{ex.StackTrace}",
                                    "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { /* Ignore if MessageBox fails */ }
                Shutdown(-1);
            }
        }

        // --- MODIFIED OnExit ---
        protected override async void OnExit(ExitEventArgs e) // Made async
        {
            _logger?.LogInfo($"Application attempting to save settings before exiting...");
            try
            {
                if (_serviceProvider != null)
                {
                    var configService = _serviceProvider.GetService<IConfigurationService>();
                    if (configService != null)
                    {
                        await configService.SaveSettingsAsync();
                        _logger?.LogInfo("Settings saved successfully.");
                    }
                    else
                    {
                        _logger?.LogError("Could not resolve ConfigurationService to save settings on exit.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to save settings on exit: {ex.Message}", ex);
                // Optionally inform the user, but might be too late if app is closing forcefully
            }

            _logger?.LogInfo($"Application exiting with code {e.ApplicationExitCode}.");
            // Dispose the service provider
            _serviceProvider?.Dispose(); // Dispose after saving attempt
            base.OnExit(e);
        }
        // --- END MODIFIED OnExit ---
    }
}