using AutoTubeWpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq; // For string.Join
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks;
// using System.Windows; // No longer needed for MessageBox

namespace AutoTubeWpf.ViewModels
{
    public partial class MetadataViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly IAiService _aiService;
        private readonly IDialogService _dialogService; // Added Dialog service
        private CancellationTokenSource? _metadataCts;

        // --- UI Bound Properties ---

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateHashtagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTitlesCommand))]
        private string? _contextText;

        [ObservableProperty]
        private int _hashtagCount = 10;
        [ObservableProperty]
        private int _tagCount = 15;
        [ObservableProperty]
        private int _titleCount = 5;

        [ObservableProperty]
        private string? _hashtagsOutput;
        [ObservableProperty]
        private string? _tagsOutput;
        [ObservableProperty]
        private string? _titlesOutput;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateHashtagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTitlesCommand))]
        private bool _isGeneratingHashtags = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateHashtagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTitlesCommand))]
        private bool _isGeneratingTags = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateHashtagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTitlesCommand))]
        private bool _isGeneratingTitles = false;


        // --- Constructor ---
        public MetadataViewModel(
            ILoggerService loggerService,
            IAiService aiService,
            IDialogService dialogService) // Added Dialog service
        {
            _logger = loggerService;
            _aiService = aiService;
            _dialogService = dialogService; // Store Dialog service
            _logger.LogInfo("MetadataViewModel initialized.");
        }

        // --- Commands ---

        // Generic helper to run generation and handle common logic
        private async Task GenerateMetadataInternalAsync(
            string type,
            int count,
            Action<bool> setIsGenerating,
            Action<string?> setOutput,
            Func<bool> getIsGenerating)
        {
            if (string.IsNullOrWhiteSpace(ContextText))
            {
                 _dialogService.ShowWarningDialog("Please enter context text before generating.", "Input Required");
                 return;
            }
            if (count <= 0)
            {
                 _dialogService.ShowWarningDialog($"Please set a positive count for {type}.", "Input Required");
                 return;
            }

            _logger.LogInfo($"Generate{type.CapitalizeFirst()} command executed.");
            _metadataCts = new CancellationTokenSource();
            setIsGenerating(true);
            NotifyCommandExecutionChanged();

            try
            {
                setOutput($"Generating {type}...");
                List<string>? results = await _aiService.GenerateMetadataAsync(type.ToLowerInvariant(), ContextText, count, _metadataCts.Token);

                if (results != null)
                {
                    setOutput(string.Join(Environment.NewLine, results));
                    _logger.LogInfo($"{type.CapitalizeFirst()} generated successfully.");
                    if (!results.Any())
                    {
                         _dialogService.ShowInfoDialog($"The AI returned no {type} based on the provided context.", "Empty Result");
                    }
                }
                else
                {
                    setOutput($"{type.CapitalizeFirst()} generation failed or returned empty.");
                    _logger.LogWarning($"{type.CapitalizeFirst()} generation failed or returned empty.");
                    _dialogService.ShowWarningDialog($"{type.CapitalizeFirst()} generation failed or returned an empty result. Check logs for details.", "Generation Failed");
                }
            }
            catch (OperationCanceledException)
            {
                setOutput($"{type.CapitalizeFirst()} generation cancelled.");
                _logger.LogInfo($"{type.CapitalizeFirst()} generation was cancelled by the user.");
                _dialogService.ShowInfoDialog($"{type.CapitalizeFirst()} generation was cancelled.", "Cancelled");
            }
            catch (InvalidOperationException ioex)
            {
                 setOutput($"{type.CapitalizeFirst()} generation failed: {ioex.Message}");
                 _logger.LogError($"{type.CapitalizeFirst()} generation failed: {ioex.Message}", ioex);
                 _dialogService.ShowErrorDialog($"{type.CapitalizeFirst()} generation failed:\n{ioex.Message}\n\nPlease ensure the AI Service is configured correctly in Settings.", "Service Error");
            }
            catch (Exception ex)
            {
                setOutput($"{type.CapitalizeFirst()} generation failed: {ex.Message}");
                _logger.LogError($"An unexpected error occurred during {type} generation.", ex);
                _dialogService.ShowErrorDialog($"An unexpected error occurred during {type} generation:\n{ex.Message}", "Error");
            }
            finally
            {
                setIsGenerating(false);
                _metadataCts?.Dispose();
                _metadataCts = null;
                NotifyCommandExecutionChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanGenerateMetadata))]
        private Task GenerateHashtagsAsync() => GenerateMetadataInternalAsync("Hashtags", HashtagCount, v => IsGeneratingHashtags = v, v => HashtagsOutput = v, () => IsGeneratingHashtags);

        [RelayCommand(CanExecute = nameof(CanGenerateMetadata))]
        private Task GenerateTagsAsync() => GenerateMetadataInternalAsync("Tags", TagCount, v => IsGeneratingTags = v, v => TagsOutput = v, () => IsGeneratingTags);

        [RelayCommand(CanExecute = nameof(CanGenerateMetadata))]
        private Task GenerateTitlesAsync() => GenerateMetadataInternalAsync("Titles", TitleCount, v => IsGeneratingTitles = v, v => TitlesOutput = v, () => IsGeneratingTitles);

        private bool CanGenerateMetadata()
        {
            return !IsGeneratingHashtags &amp;&amp; !IsGeneratingTags &amp;&amp; !IsGeneratingTitles
                &amp;&amp; !string.IsNullOrWhiteSpace(ContextText)
                &amp;&amp; _aiService.IsAvailable;
        }

        // Helper to notify CanExecuteChanged for all commands
        private void NotifyCommandExecutionChanged()
        {
            GenerateHashtagsCommand.NotifyCanExecuteChanged();
            GenerateTagsCommand.NotifyCanExecuteChanged();
            GenerateTitlesCommand.NotifyCanExecuteChanged();
        }
    }

     // Helper extension method (can be moved to a shared Utils file later)
    internal static class StringExtensions
    {
        internal static string CapitalizeFirst(this string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}