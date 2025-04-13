using AutoTubeWpf.Models;
using AutoTubeWpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows; // For MessageBox - replace with DialogService later

namespace AutoTubeWpf.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILoggerService _loggerService;

        // Observable properties bound to the UI
        [ObservableProperty]
        private string? _googleApiKey;

        [ObservableProperty]
        private string? _awsAccessKeyId;

        [ObservableProperty]
        private string? _awsSecretAccessKey; // Consider using SecureString or PasswordBox binding in View

        [ObservableProperty]
        private string? _awsRegionName;

        [ObservableProperty]
        private string? _defaultOutputPath;

        [ObservableProperty]
        private bool _organizeOutput;

        [ObservableProperty]
        private string? _selectedTheme; // e.g., "dark", "light", "system"

        // Available themes for ComboBox
        public string[] AvailableThemes { get; } = { "dark", "light", "system" };

        public SettingsViewModel(IConfigurationService configurationService, ILoggerService loggerService)
        {
            _configurationService = configurationService;
            _loggerService = loggerService;

            _loggerService.LogInfo("SettingsViewModel initializing...");
            LoadSettings();
            _loggerService.LogInfo("SettingsViewModel initialized.");
        }

        private void LoadSettings()
        {
            _loggerService.LogDebug("Loading settings into SettingsViewModel...");
            var settings = _configurationService.CurrentSettings;
            GoogleApiKey = settings.GoogleApiKey;
            AwsAccessKeyId = settings.AwsAccessKeyId;
            AwsSecretAccessKey = settings.AwsSecretAccessKey; // Be mindful of exposing secrets directly
            AwsRegionName = settings.AwsRegionName;
            DefaultOutputPath = settings.DefaultOutputPath;
            OrganizeOutput = settings.OrganizeOutput;
            SelectedTheme = settings.Theme;
             _loggerService.LogDebug("Settings loaded into SettingsViewModel.");
       }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            _loggerService.LogInfo("SaveSettings command executed.");
            try
            {
                // Update the settings object before saving
                var settings = _configurationService.CurrentSettings;
                settings.GoogleApiKey = GoogleApiKey;
                settings.AwsAccessKeyId = AwsAccessKeyId;
                settings.AwsSecretAccessKey = AwsSecretAccessKey;
                settings.AwsRegionName = AwsRegionName;
                settings.DefaultOutputPath = DefaultOutputPath;
                settings.OrganizeOutput = OrganizeOutput;
                settings.Theme = SelectedTheme ?? "dark"; // Ensure theme is not null

                await _configurationService.SaveSettingsAsync();
                _loggerService.LogInfo("Settings saved successfully via ConfigurationService.");

                // TODO: Trigger re-configuration of dependent services (API clients, ThemeManager)
                // Example: _apiClientService.Reconfigure();
                // Example: _themeManager.ApplyTheme(settings.Theme);

                // Replace MessageBox with a proper dialog service later
                MessageBox.Show("Settings saved successfully.", "Save Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to save settings.", ex);
                 // Replace MessageBox with a proper dialog service later
               MessageBox.Show($"Failed to save settings:\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void BrowseOutputFolder()
        {
             _loggerService.LogDebug("BrowseOutputFolder command executed.");
            // Using Ookii.Dialogs.Wpf for a better folder browser dialog
            // Requires adding the Ookii.Dialogs.Wpf NuGet package
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = "Select Default Output Folder",
                UseDescriptionForTitle = true
            };

            if (!string.IsNullOrWhiteSpace(DefaultOutputPath) && System.IO.Directory.Exists(DefaultOutputPath))
            {
                dialog.SelectedPath = DefaultOutputPath;
            }
            else
            {
                 dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }


            if (dialog.ShowDialog() == true)
            {
                DefaultOutputPath = dialog.SelectedPath;
                _loggerService.LogInfo($"Default output path selected: {DefaultOutputPath}");
            }
             else
            {
                 _loggerService.LogDebug("Output folder selection cancelled.");
            }
        }
    }
}