using AutoTubeWpf.Models;
using System.Threading.Tasks;

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines the contract for managing application settings persistence.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the currently loaded application settings.
        /// Returns default settings if not loaded yet or if loading failed.
        /// </summary>
        AppSettings CurrentSettings { get; }

        /// <summary>
        /// Asynchronously loads the application settings from the configuration file.
        /// If the file doesn't exist, it initializes with default settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LoadSettingsAsync();

        /// <summary>
        /// Asynchronously saves the current application settings to the configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveSettingsAsync();

        /// <summary>
        /// Gets the full path to the configuration file.
        /// </summary>
        string GetConfigFilePath();
    }
}