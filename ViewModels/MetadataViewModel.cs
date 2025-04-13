using AutoTubeWpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq; // For string.Join
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks;
using System.Windows; // For MessageBox - replace with DialogService later

namespace AutoTubeWpf.ViewModels
{
    public partial class MetadataViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly IAiService _aiService; // Added AI service
        private CancellationTokenSource? _metadataCts; // Single CTS for all metadata generation types

        // --- UI Bound Properties ---

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateHashtagsCommand))] // Update CanExecute when context changes
        [NotifyCanExecuteChangedFor(nameof(GenerateTagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateTitlesCommand))]
        private string? _contextText; // Input text for generation

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
        public MetadataViewModel(ILoggerService loggerService, IAiService aiService) // Added IAiService
        {
            _logger = loggerService;
            _aiService = aiService; // Store AI service
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
                 MessageBox.Show("Please enter context text before generating.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }
            if (count <= 0)
            {
                 MessageBox.Show($"Please set a positive count for {type}.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }

            _logger.LogInfo($"Generate{type.CapitalizeFirst()} command executed.");
            _metadataCts = new CancellationTokenSource();
            setIsGenerating(true);
            NotifyCommandExecutionChanged(); // Update CanExecute for all buttons

            try
            {
                setOutput($"Generating {type}..."); // Indicate activity
                List<string>? results = await _aiService.GenerateMetadataAsync(type.ToLowerInvariant(), ContextText, count, _metadataCts.Token);

                if (results != null)
                {
                    setOutput(string.Join(Environment.NewLine, results)); // Join list items with newlines
                    _logger.LogInfo($"{type.CapitalizeFirst()} generated successfully.");
                    if (!results.Any())
                    {
                         MessageBox.Show($"The AI returned no {type} based on the provided context.", "Empty Result", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    setOutput($"{type.CapitalizeFirst()} generation failed or returned empty.");
                    _logger.LogWarning($"{type.CapitalizeFirst()} generation failed or returned empty.");
                    MessageBox.Show($"{type.CapitalizeFirst()} generation failed or returned an empty result. Check logs for details.", "Generation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                setOutput($"{type.CapitalizeFirst()} generation cancelled.");
                _logger.LogInfo($"{type.CapitalizeFirst()} generation was cancelled by the user.");
                MessageBox.Show($"{type.CapitalizeFirst()} generation was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ioex) // Catch if service not available
            {
                 setOutput($"{type.CapitalizeFirst()} generation failed: {ioex.Message}");
                 _logger.LogError($"{type.CapitalizeFirst()} generation failed: {ioex.Message}", ioex);
                 MessageBox.Show($"{type.CapitalizeFirst()} generation failed:\n{ioex.Message}\n\nPlease ensure the AI Service is configured correctly in Settings.", "Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                setOutput($"{type.CapitalizeFirst()} generation failed: {ex.Message}");
                _logger.LogError($"An unexpected error occurred during {type} generation.", ex);
                MessageBox.Show($"An unexpected error occurred during {type} generation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                setIsGenerating(false);
                _metadataCts?.Dispose();
                _metadataCts = null;
                NotifyCommandExecutionChanged(); // Update CanExecute for all buttons
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
            return !IsGeneratingHashtags &amp;&amp; !IsGeneratingTags &amp;&amp; !IsGeneratingTitles // Not currently generating any
                &amp;&amp; !string.IsNullOrWhiteSpace(ContextText) // Context is provided
                &amp;&amp; _aiService.IsAvailable; // AI service is ready
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