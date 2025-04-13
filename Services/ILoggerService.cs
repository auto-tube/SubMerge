using System;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines the contract for application-wide logging.
    /// </summary>
    public interface ILoggerService
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message, Exception? exception = null);
        void LogError(string message, Exception? exception = null);
        void LogCritical(string message, Exception? exception = null);
    }
}