using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Provides application-wide logging services using Microsoft.Extensions.Logging.
    /// Logs to Debug output and a file in the application's base directory.
    /// </summary>
    public class LoggerService : ILoggerService
    {
        private readonly ILogger _logger;
        private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "autotube_wpf_log.txt");

        // Static factory to ensure it's disposed properly.
        // NOTE: In a real app using DI host builder, this factory management is handled automatically.
        private static ILoggerFactory? _loggerFactory;

        public LoggerService()
        {
            // Create factory only once
            if (_loggerFactory == null)
            {
                 // Ensure previous log file is cleared for clean testing run
                 try { if(File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch {}

                _loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder
                        .SetMinimumLevel(LogLevel.Debug) // Log everything from Debug up
                        .AddDebug() // Log to VS Debug Output
                        .AddFile(LogFilePath, minimumLevel: LogLevel.Debug); // Log to File
                });

                 // Optional: Hook into application exit to dispose factory if needed,
                 // though singleton lifetime managed by DI might handle this.
                 // AppDomain.CurrentDomain.ProcessExit += (s, e) => _loggerFactory?.Dispose();
            }

            // Create the logger instance for this service instance
            _logger = _loggerFactory.CreateLogger("AutoTubeWpf"); // Use a category name

            LogInfo($"LoggerService instance created. Logging to: {LogFilePath}");
        }

        // --- Log methods remain the same ---

        public void LogDebug(string message)
        {
            _logger.LogDebug(message);
        }

        public void LogInfo(string message)
        {
            _logger.LogInformation(message);
        }

        public void LogWarning(string message, Exception? exception = null)
        {
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
             if (exception != null)
            {
                _logger.LogCritical(exception, message);
            }
            else
            {
                _logger.LogCritical(message);
            }
        }
    }
}