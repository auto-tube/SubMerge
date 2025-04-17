using AutoTubeWpf.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics; // For basic logging fallback

namespace AutoTubeWpf.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private const string AppName = "Autotube"; // Consistent with Python version
        private const string ConfigFilename = "autotube_config.json"; // Consistent with Python version

        private AppSettings _currentSettings;
        private readonly string _configFilePath;

        // Lazy initialization for default settings
        public AppSettings CurrentSettings => _currentSettings ??= new AppSettings();

        public ConfigurationService(/* Consider injecting ILogger later */)
        {
            _configFilePath = DetermineConfigPath();
            // Initialize with defaults, LoadSettingsAsync should be called explicitly later
            _currentSettings = new AppSettings();
            Debug.WriteLine($"[ConfigurationService] Config path determined: {_configFilePath}");
        }

        public string GetConfigFilePath() => _configFilePath;

        private string DetermineConfigPath()
        {
            string? configDir = null;
            try
            {
                // Prefer AppData/Roaming on Windows (similar to Python's %APPDATA%)
                if (OperatingSystem.IsWindows())
                {
                    configDir = Environment.GetEnvironmentVariable("APPDATA");
                }
                // Use standard locations on other platforms (can be refined)
                else if (OperatingSystem.IsMacOS())
                {
                    // Typically ~/Library/Application Support/
                    string? home = Environment.GetEnvironmentVariable("HOME");
                    if (!string.IsNullOrEmpty(home))
                    {
                        configDir = Path.Combine(home, "Library", "Application Support");
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Typically ~/.config/ or ~/.local/share/
                    string? xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    // --- CORRECTED LINE ---
                    if (!string.IsNullOrEmpty(xdgConfigHome) && Directory.Exists(xdgConfigHome))
                    // --- END CORRECTED LINE ---
                    {
                        configDir = xdgConfigHome;
                    }
                    else
                    {
                        string? home = Environment.GetEnvironmentVariable("HOME");
                        if (!string.IsNullOrEmpty(home))
                        {
                            configDir = Path.Combine(home, ".config");
                            if (!Directory.Exists(configDir))
                            {
                                configDir = Path.Combine(home, ".local", "share");
                            }
                        }
                    }
                }

                // Fallback to user's home directory if standard locations fail
                if (string.IsNullOrEmpty(configDir) || !Directory.Exists(configDir))
                {
                    configDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    Debug.WriteLine($"[ConfigurationService] Warning: Could not determine standard config directory, using user profile: {configDir}");
                }

                string appConfigDir = Path.Combine(configDir, AppName);
                return Path.Combine(appConfigDir, ConfigFilename);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error determining config path: {ex.Message}. Falling back to current directory.");
                // Fallback to current directory if everything else fails
                return Path.GetFullPath(ConfigFilename);
            }
        }

        public async Task LoadSettingsAsync()
        {
            Debug.WriteLine($"[ConfigurationService] Attempting to load settings from: {_configFilePath}");
            if (!File.Exists(_configFilePath))
            {
                Debug.WriteLine("[ConfigurationService] Config file not found. Initializing with default settings.");
                _currentSettings = new AppSettings(); // Ensure defaults are loaded
                // Set default output path if empty (similar to Python logic)
                if (string.IsNullOrEmpty(_currentSettings.DefaultOutputPath))
                {
                    _currentSettings.DefaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Autotube_Output");
                    Debug.WriteLine($"[ConfigurationService] Default output path set to: {_currentSettings.DefaultOutputPath}");
                }
                return; // Nothing more to do if file doesn't exist
            }

            try
            {
                using FileStream openStream = File.OpenRead(_configFilePath);
                // --- CORRECTED LINE --- (Removed PropertyNameCaseInsensitive which might cause issues if casing differs)
                var loadedSettings = await JsonSerializer.DeserializeAsync<AppSettings>(openStream);
                // --- END CORRECTED LINE ---

                _currentSettings = loadedSettings ?? new AppSettings(); // Use defaults if deserialization returns null
                Debug.WriteLine("[ConfigurationService] Settings loaded successfully.");

                // Set default output path if empty after loading
                if (string.IsNullOrEmpty(_currentSettings.DefaultOutputPath))
                {
                     _currentSettings.DefaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Autotube_Output");
                     Debug.WriteLine($"[ConfigurationService] Default output path set after loading: {_currentSettings.DefaultOutputPath}");
                }
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"[ConfigurationService] Error: Invalid JSON in config file '{_configFilePath}'. Using default settings. Details: {jsonEx.Message}");
                _currentSettings = new AppSettings(); // Reset to defaults on error
                 // Set default output path on error
                _currentSettings.DefaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Autotube_Output");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error loading settings file '{_configFilePath}'. Using default settings. Details: {ex.Message}");
                _currentSettings = new AppSettings(); // Reset to defaults on error
                 // Set default output path on error
                _currentSettings.DefaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Autotube_Output");
            }
        }

        public async Task SaveSettingsAsync()
        {
            Debug.WriteLine($"[ConfigurationService] Attempting to save settings to: {_configFilePath}");
            try
            {
                // Ensure the directory exists
                string? directory = Path.GetDirectoryName(_configFilePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                else
                {
                     throw new IOException("Could not determine directory for config file.");
                }

                using FileStream createStream = File.Create(_configFilePath);
                await JsonSerializer.SerializeAsync(createStream, CurrentSettings, new JsonSerializerOptions { WriteIndented = true }); // Pretty print
                await createStream.DisposeAsync(); // Ensure file is closed properly
                Debug.WriteLine("[ConfigurationService] Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigurationService] Error saving settings file '{_configFilePath}'. Details: {ex.Message}");
                // Consider notifying the user here via a dialog service or event
                throw; // Re-throw so the caller knows saving failed
            }
        }
    }
}