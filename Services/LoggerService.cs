using Microsoft.Extensions.Logging; // Required for ILogger
using System;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Provides application-wide logging services using Microsoft.Extensions.Logging.
    /// Currently logs to the Debug output.
    /// </summary>
    public class LoggerService : ILoggerService
    {
        private readonly ILogger _logger;

        public LoggerService()
        {
            // Set up the logger factory
            // In a more complex app with DI, this would be configured elsewhere.
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning) // Filter out noisy framework logs
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("AutoTubeWpf", LogLevel.Debug) // Log our app messages from Debug level up
                    .AddDebug(); // Add Debug output provider
                // TODO: Add File logging provider here later (e.g., Serilog, NLog)
            });

            // Create the logger instance for this service
            _logger = loggerFactory.CreateLogger<LoggerService>();

            LogInfo("LoggerService initialized.");
        }

        public void LogDebug(string message)
        {
            _logger.LogDebug(message);
        }

        public void LogInfo(string message)
        {
            _logger.LogInformation(message); // Note: LogInformation maps to Info level
        }

        public void LogWarning(string message, Exception? exception = null)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, message); // Log exception details with the message
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