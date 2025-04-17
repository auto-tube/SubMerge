using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics; // Ensure System.Diagnostics is included
using System.IO;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Provides application-wide logging services using Microsoft.Extensions.Logging.
    /// Logs to Debug output and a file in the application's base directory.
    /// </summary>
    // Implement IDisposable to manage the factory lifetime
    public class LoggerService : ILoggerService, IDisposable 
    {
        private ILogger _logger;
        private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "autotube_wpf_log.txt");

        // Static factory to ensure it's disposed properly. Make it nullable.
        private static ILoggerFactory? _loggerFactory;
        private bool _disposed = false; // Track disposal status

        public LoggerService()
        {
            EnsureLoggerFactoryCreated(); // Ensure factory exists
            _logger = _loggerFactory!.CreateLogger("AutoTubeWpf"); // Use category name, non-null asserted by Ensure method
            LogInfo($"LoggerService instance created. Logging to: {LogFilePath}");
        }

        private static void EnsureLoggerFactoryCreated(bool forceRecreate = false)
        {
             // Force recreation or create if null
            if (forceRecreate || _loggerFactory == null)
            {
                // Dispose existing factory if forcing recreation
                // Corrected && below
                if (forceRecreate && _loggerFactory != null) 
                {
                    Debug.WriteLine("[LoggerService] Disposing existing ILoggerFactory...");
                    _loggerFactory.Dispose();
                    _loggerFactory = null;
                }

                Debug.WriteLine($"[LoggerService] Attempting to create ILoggerFactory (forceRecreate={forceRecreate})..."); 
                // Ensure previous log file is cleared ONLY on initial creation, not recreation
                if (!forceRecreate) 
                {
                    try
                    {
                        if(File.Exists(LogFilePath))
                        {
                             Debug.WriteLine($"[LoggerService] Deleting existing log file: {LogFilePath}"); 
                             File.Delete(LogFilePath);
                        }
                    }
                    catch (Exception exDel)
                    {
                         Debug.WriteLine($"[LoggerService] WARNING: Failed to delete existing log file: {exDel.Message}"); 
                    }
                }

                try 
                {
                    _loggerFactory = LoggerFactory.Create(builder =>
                    {
                        Debug.WriteLine("[LoggerService] Configuring LoggerFactory builder..."); 
                        builder
                            .SetMinimumLevel(LogLevel.Debug) // Log everything from Debug up
                            .AddDebug() // Log to VS Debug Output
                            .AddFile(LogFilePath, minimumLevel: LogLevel.Debug); // Log to File
                        Debug.WriteLine("[LoggerService] LoggerFactory builder configured."); 
                    });
                    Debug.WriteLine("[LoggerService] ILoggerFactory created successfully."); 
                }
                catch (Exception exFactory)
                {
                    Debug.WriteLine($"[LoggerService] FATAL ERROR creating ILoggerFactory: {exFactory}"); 
                    throw; // Re-throw to make the failure visible
                }
            }
            else
            {
                Debug.WriteLine("[LoggerService] ILoggerFactory already exists."); 
            }
        }

        // Method to allow reconfiguring (e.g., after settings change)
        public static void ReconfigureLogging()
        {
            EnsureLoggerFactoryCreated(forceRecreate: true);
        }

        // --- Log methods ---

        public void LogDebug(string message)
        {
            _logger?.LogDebug(message); // Add null check in case logger failed
        }

        public void LogInfo(string message)
        {
            _logger?.LogInformation(message); // Add null check
        }

        public void LogWarning(string message, Exception? exception = null)
        {
            if (_logger == null) return; // Add null check
            if (exception != null)
            {
                _logger.LogWarning(exception, message);
            }
            else
            {
                _logger.LogWarning(message);
            }
        }

        public void LogError(string message, Exception? exception = null)
        {
             if (_logger == null) return; // Add null check
             if (exception != null)
            {
                _logger.LogError(exception, message);
            }
            else
            {
                _logger.LogError(message);
            }
        }

        public void LogCritical(string message, Exception? exception = null)
        {
             if (_logger == null) return; // Add null check
             if (exception != null)
            {
                _logger.LogCritical(exception, message);
            }
            else
            {
                _logger.LogCritical(message);
            }
        }

        // --- IDisposable Implementation ---
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    // The static factory should ideally be disposed when the App exits.
                    // We might need a static Dispose method or handle it in App.OnExit.
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // Static method to dispose the factory, call this from App.OnExit
        public static void DisposeFactory()
        {
             Debug.WriteLine("[LoggerService] Disposing static ILoggerFactory...");
            _loggerFactory?.Dispose();
            _loggerFactory = null;
        }
    }
}