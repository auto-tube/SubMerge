using AutoTubeWpf.Models;
using AutoTubeWpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System; // Added for Environment
using System.IO; // Added for Directory
using System.Threading.Tasks;
// using System.Windows; // No longer needed for MessageBox

namespace AutoTubeWpf.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILoggerService _loggerService;
        private readonly IDialogService _dialogService;
        private readonly IAiService _aiService; // Added AI service

        // Observable properties bound to the UI
        [ObservableProperty]
        private string? _googleApiKey;

        [ObservableProperty]
        private string? _awsAccessKeyId;

        [ObservableProperty]
        private string? _awsSecretAccessKey;

        [ObservableProperty]
        private string? _awsRegionName;

        [ObservableProperty]
        private string? _defaultOutputPath;

        [ObservableProperty]
        private bool _organizeOutput;

        [ObservableProperty]
        private string? _selectedTheme;

        public string[] AvailableThemes { get; } = { "dark", "light", "system" };

        public SettingsViewModel(
            IConfigurationService configurationService,
            ILoggerService loggerService,
            IDialogService dialogService,
            IAiService aiService) // Added AI service
        {
            _configurationService = configurationService;
            _loggerService = loggerService;
            _dialogService = dialogService;
            _aiService = aiService; // Store AI service

            _loggerService.LogInfo("SettingsViewModel initializing...");
            // Log if the injected service is null
            if (_aiService == null) _loggerService.LogError("IAiService is NULL in SettingsViewModel constructor!");
            else _loggerService.LogDebug("IAiService successfully injected into SettingsViewModel.");
            LoadSettings();
            _loggerService.LogInfo("SettingsViewModel initialized.");
        }

        private void LoadSettings()
        {
            _loggerService.LogDebug("Loading settings into SettingsViewModel...");
            var settings = _configurationService.CurrentSettings;
            GoogleApiKey = settings.GoogleApiKey;
            AwsAccessKeyId = settings.AwsAccessKeyId;
            AwsSecretAccessKey = settings.AwsSecretAccessKey;
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
                settings.GoogleApiKey = GoogleApiKey; // Still save the key to settings if needed elsewhere
                settings.AwsAccessKeyId = AwsAccessKeyId;
                settings.AwsSecretAccessKey = AwsSecretAccessKey;
                settings.AwsRegionName = AwsRegionName;
                settings.DefaultOutputPath = DefaultOutputPath;
                settings.OrganizeOutput = OrganizeOutput;
                settings.Theme = SelectedTheme ?? "dark";

                await _configurationService.SaveSettingsAsync();
                _loggerService.LogInfo("Settings saved successfully via ConfigurationService.");

                // Re-configure dependent services
                _loggerService.LogInfo("Re-configuring AI Service..."); // Updated log message
                if (_aiService == null)
                {
                    _loggerService.LogError("Cannot re-configure AI Service: _aiService is NULL in SaveSettingsAsync!");
                }
                else
                {
                    _aiService.Configure(); // Call parameterless Configure
                    _loggerService.LogDebug($"AI Service IsAvailable after re-configure: {_aiService.IsAvailable}"); // Log availability status
                }
                // TODO: Add similar calls for other services like ITtsService if needed

                _dialogService.ShowInfoDialog("Settings saved and services reconfigured.", "Save Settings"); // Updated message
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to save settings.", ex);
                _dialogService.ShowErrorDialog($"Failed to save settings:\n\n{ex.Message}", "Save Error"); // Use DialogService
            }
        }

        [RelayCommand]
        private void BrowseOutputFolder()
        {
             _loggerService.LogDebug("BrowseOutputFolder command executed.");
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = "Select Default Output Folder",
                UseDescriptionForTitle = true
            };

            if (!string.IsNullOrWhiteSpace(DefaultOutputPath) && Directory.Exists(DefaultOutputPath))
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