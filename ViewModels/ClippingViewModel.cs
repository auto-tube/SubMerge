using AutoTubeWpf.Models;
using AutoTubeWpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic; // For List
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks;
using System.Windows.Threading; // For DispatcherTimer

namespace AutoTubeWpf.ViewModels
{
    // Simple model placeholder for items in the queue
    public partial class VideoQueueItem : ObservableObject
    {
        [ObservableProperty]
        private string? _filePath;
        [ObservableProperty]
        private string? _fileName;
        [ObservableProperty]
        private string? _status; // e.g., Pending, Processing, Done, Error
        [ObservableProperty]
        private TimeSpan? _duration; // Optional: To be fetched later

        public VideoQueueItem(string path)
        {
            FilePath = path;
            FileName = Path.GetFileName(path);
            Status = "Pending";
        }
    }


    public partial class ClippingViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly IVideoProcessorService _videoProcessorService;
        private readonly IConfigurationService _configurationService;
        private readonly IFileOrganizerService _fileOrganizerService;
        private readonly IDialogService _dialogService;
        private readonly IProgress<VideoProcessingProgress>? _progressReporter; // Restore IProgress field
        private CancellationTokenSource? _clippingCts;
        // Timer removed

        // --- UI Bound Properties ---

        [ObservableProperty]
        private bool _isSeeking = false; // Flag to prevent slider updates during drag (set by slider behaviors)
        [ObservableProperty]
        private string? _inputDisplayPath;

        [ObservableProperty]
        private bool _isBatchMode = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartStopClippingCommand))]
        private ObservableCollection<VideoQueueItem> _videoQueue = new();

        // Clipping Options
        [ObservableProperty]
        private int _minClipLength = 15;
        [ObservableProperty]
        private int _maxClipLength = 45;
        [ObservableProperty]
        private double _sceneThreshold = 30.0;
        [ObservableProperty]
        private int _clipCount = 5;
        [ObservableProperty]
        private bool _useSceneDetection = false;
        [ObservableProperty]
        private bool _formatAsVertical = true;
        [ObservableProperty]
        private bool _removeAudio = false;
        [ObservableProperty]
        private bool _mirrorVideo = false;
        [ObservableProperty]
        private bool _enhanceVideo = true;


        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartStopClippingCommand))]
        [NotifyCanExecuteChangedFor(nameof(ClearQueueCommand))]
        private bool _isProcessing = false;

        // --- Video Player Properties ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayerPositionFormatted))]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private VideoQueueItem? _selectedVideoItem; // Bound to DataGrid SelectedItem

        [ObservableProperty]
        private Uri? _playerSource; // Bound to MediaElement Source

        [ObservableProperty]
        private double _playerDurationSeconds = 1.0; // Total duration for slider max (default 1 to avoid div by zero)

        [ObservableProperty]
        private double _playerPositionSeconds = 0.0; // Current position for slider value

        [ObservableProperty]
        private double _playerSpeedRatio = 1.0; // For playback speed slider

        [ObservableProperty]
        private bool _isPlayerPlaying = false; // Tracks if player should be playing

        public string PlayerPositionFormatted => TimeSpan.FromSeconds(PlayerPositionSeconds).ToString(@"hh\:mm\:ss");


        // --- Services ---
        public ClippingViewModel(
            ILoggerService loggerService,
            IVideoProcessorService videoProcessorService,
            IConfigurationService configService,
            IFileOrganizerService fileOrganizerService,
            IDialogService dialogService,
            IProgress<VideoProcessingProgress>? progressReporter = null) // Restore IProgress injection
       {
           _logger = loggerService;
           _videoProcessorService = videoProcessorService;
           _configurationService = configService;
           _fileOrganizerService = fileOrganizerService;
           _dialogService = dialogService;
           _progressReporter = progressReporter; // Restore assignment
            _logger.LogInfo("ClippingViewModel initialized.");

            // Timer initialization removed
        }

        // --- Partial Property Changed Methods ---
        partial void OnSelectedVideoItemChanged(VideoQueueItem? value)
        {
            _logger.LogDebug($"SelectedVideoItem changed: {value?.FileName ?? "null"}");
            // Stop previous playback
            IsPlayerPlaying = false; // This should trigger behavior to stop MediaElement
            // Timer stop removed
            PlayerPositionSeconds = 0;
            PlayerDurationSeconds = 1.0; // Reset duration (will be updated by MediaOpened behavior)

            if (value?.FilePath != null && File.Exists(value.FilePath)) // Corrected &&
            {
                try
                {
                    PlayerSource = new Uri(value.FilePath);
                    _logger.LogInfo($"Player source set to: {PlayerSource}");
                    // Duration will be updated by MediaOpened behavior calling MediaOpenedCommand
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error setting player source for {value.FilePath}: {ex.Message}", ex);
                    PlayerSource = null;
                    _dialogService.ShowErrorDialog($"Could not load video for preview:\n{ex.Message}", "Preview Error");
                }
            }
            else
            {
                PlayerSource = null;
                if (value != null)
                {
                     _logger.LogWarning($"Selected video file path is invalid or null: {value.FilePath}");
                }
            }
            // Notify commands that depend on selection
            PlayPauseCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
        }

        // --- Player Timer Removed ---


        // --- Commands ---

        [RelayCommand]
        private void SelectInput()
        {
            _logger.LogInfo("--- SelectInput command invoked ---"); // Added for debugging
            _logger.LogDebug("SelectInput command executed.");
            try
            {
                if (IsBatchMode)
                {
                    var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { Description = "Select Folder Containing Videos", UseDescriptionForTitle = true };
                    if (dialog.ShowDialog() == true) AddDirectoryToQueue(dialog.SelectedPath);
                }
                else
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select Video File(s)", Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv|All Files|*.*", Multiselect = true };
                    if (dialog.ShowDialog() == true) AddFilesToQueue(dialog.FileNames);
                }
            }
            catch (Exception ex) { _logger.LogError("Error during input selection.", ex); _dialogService.ShowErrorDialog($"An error occurred selecting input:\n{ex.Message}", "Input Error"); }
            UpdateInputDisplayPath();
        }

        [RelayCommand(CanExecute = nameof(CanClearQueue))]
        private void ClearQueue()
        {
            _logger.LogInfo("ClearQueue command executed.");
             if (_dialogService.ShowConfirmationDialog($"Are you sure you want to remove all {VideoQueue.Count} video(s) from the queue?", "Confirm Clear Queue") == DialogResult.Yes)
            {
                // Stop playback if clearing queue
                IsPlayerPlaying = false; // Should trigger behavior to stop MediaElement
                PlayerPositionSeconds = 0;
                VideoQueue.Clear();
                SelectedVideoItem = null; // Clear selection
                UpdateInputDisplayPath();
                _logger.LogInfo("Video queue cleared.");
            }
        }
        private bool CanClearQueue() => VideoQueue.Any() && !IsProcessing; // Corrected &&

        [RelayCommand(CanExecute = nameof(CanPlayPause))]
        private void PlayPause()
        {
            IsPlayerPlaying = !IsPlayerPlaying; // Toggle state. Behavior in XAML will react to this.
            // Timer start/stop removed
            _logger.LogDebug($"Play/Pause command executed. IsPlaying set to: {IsPlayerPlaying}");
        }
        private bool CanPlayPause() => SelectedVideoItem != null;

        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            // Set state, behavior in XAML will react
            IsPlayerPlaying = false;
            PlayerPositionSeconds = 0;
            _logger.LogDebug("Stop command executed.");
        }
        private bool CanStop() => SelectedVideoItem != null;

        // StopPlayer method removed

        // Methods called from code-behind removed (StartSeek, EndSeek, SetMediaDuration, UpdatePlayerPosition, MediaPlaybackEnded)
        // These will be replaced by commands triggered by XAML behaviors


        // --- Commands for XAML Behaviors ---

        [RelayCommand]
        private void MediaOpened(TimeSpan duration) // Parameter passed from behavior if possible, otherwise read from property
        {
            // This command will be triggered by MediaElement.MediaOpened event via Behavior
            PlayerDurationSeconds = duration.TotalSeconds;
            _logger.LogDebug($"Media opened, duration set to: {duration}");
        }

        [RelayCommand]
        private void MediaEnded()
        {
             // This command will be triggered by MediaElement.MediaEnded event via Behavior
             IsPlayerPlaying = false;
             PlayerPositionSeconds = 0; // Optionally reset position on end
             _logger.LogDebug("Media ended.");
        }

         [RelayCommand]
        private void MediaFailed(string? errorMessage) // Parameter passed from behavior if possible
        {
             // This command will be triggered by MediaElement.MediaFailed event via Behavior
             IsPlayerPlaying = false;
             PlayerPositionSeconds = 0;
             _logger.LogError($"Media failed: {errorMessage ?? "Unknown error"}");
             _dialogService.ShowErrorDialog($"Could not play the selected video.\nError: {errorMessage ?? "Unknown error"}", "Playback Error");
        }

        [RelayCommand]
        private void SeekDragStarted()
        {
            IsSeeking = true;
            _logger.LogDebug("Seek drag started."); // Changed from LogTrace to LogDebug
        }

        [RelayCommand]
        private void SeekDragCompleted()
        {
            IsSeeking = false;
            _logger.LogDebug("Seek drag completed."); // Changed from LogTrace to LogDebug
            // Position is already updated via TwoWay binding on Slider
        }


        // --- Main Processing Command ---

        [RelayCommand(CanExecute = nameof(CanStartStopClipping))]
        private async Task StartStopClippingAsync()
        {
            if (IsProcessing) // --- STOP ---
            {
                _logger.LogInfo("Stop Clipping command executed. Requesting cancellation...");
                try { _clippingCts?.Cancel(); }
                catch (ObjectDisposedException) { /* Ignore */ }
                catch (Exception ex) { _logger.LogError("Error requesting cancellation.", ex); }
            }
            else // --- START ---
            {
                _logger.LogInfo("Start Clipping command executed.");
                _clippingCts = new CancellationTokenSource();
                var token = _clippingCts.Token;
                IsProcessing = true;
                StartStopClippingCommand.NotifyCanExecuteChanged();

                string? outputBasePath = _configurationService.CurrentSettings.DefaultOutputPath;
                bool organizeOutput = _configurationService.CurrentSettings.OrganizeOutput;

                // --- ADDED: Ensure output directory exists ---
                if (string.IsNullOrWhiteSpace(outputBasePath))
                {
                    // If no path is set in config, use a default one in the user's profile
                    outputBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Autotube_Output");
                    _logger.LogWarning($"DefaultOutputPath not set in configuration. Using default: {outputBasePath}");
                    // Optionally update the configuration service in memory, but don't save here
                    // _configurationService.CurrentSettings.DefaultOutputPath = outputBasePath;
                }

                try
                {
                    _logger.LogInfo($"Ensuring output directory exists: {outputBasePath}");
                    Directory.CreateDirectory(outputBasePath); // Creates the directory if it doesn't exist, does nothing if it does.
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Cannot start clipping: Failed to create or access output directory '{outputBasePath}'. Error: {ex.Message}", ex);
                    _dialogService.ShowErrorDialog($"Could not create or access the output folder:\n{outputBasePath}\n\nError: {ex.Message}\n\nPlease check permissions or choose a different folder in Settings.", "Output Path Error");
                    IsProcessing = false;
                    return;
                }
                // --- END ADDED ---

                var queueCopy = VideoQueue.ToList();
                int totalVideos = queueCopy.Count;
                int videosProcessed = 0;
                int clipsGenerated = 0;
                int errorsEncountered = 0;
                var random = new Random();
                int totalClipsToAttempt = queueCopy.Sum(item => ClipCount); // Corrected =>
                int clipsAttemptedSoFar = 0;

                try
                {
                    for(int videoIndex = 0; videoIndex < totalVideos; videoIndex++) // Corrected <
                    {
                        var item = queueCopy[videoIndex];
                        if (token.IsCancellationRequested) { _logger.LogInfo("Clipping process cancelled by user."); item.Status = "Cancelled"; break; }

                        _logger.LogInfo($"Processing video {videoIndex + 1}/{totalVideos}: {item.FileName}");
                        item.Status = "Getting Duration...";
                        _progressReporter?.Report(new VideoProcessingProgress { Percentage = (double)videosProcessed / totalVideos, Message = $"Processing video {videoIndex + 1}/{totalVideos}...", CurrentItemIndex = videoIndex, TotalItems = totalVideos }); // Use injected _progressReporter

                        TimeSpan? duration = null;
                        try { duration = await _videoProcessorService.GetVideoDurationAsync(item.FilePath!, token); }
                        catch (Exception ex) { _logger.LogError($"Failed to get duration for {item.FileName}", ex); item.Status = "Error (Duration)"; errorsEncountered++; videosProcessed++; continue; }

                        if (duration == null || duration.Value.TotalSeconds <= MinClipLength) // Corrected <=
                        { _logger.LogWarning($"Skipping {item.FileName}: Duration ({duration?.TotalSeconds ?? 0}s) is too short or could not be determined."); item.Status = "Skipped (Short)"; videosProcessed++; continue; }

                        item.Duration = duration;
                        double totalSeconds = duration.Value.TotalSeconds;
                        int clipsMadeForThisVideo = 0;
                        List<SceneChangeInfo>? sceneChanges = null; // Corrected ?

                        if (UseSceneDetection)
                        {
                            item.Status = "Detecting Scenes...";
                            _logger.LogDebug($"Attempting scene detection for {item.FileName} with threshold {SceneThreshold / 100.0}");
                            try
                            {
                                double ffmpegThreshold = Math.Clamp(SceneThreshold / 100.0, 0.01, 1.0);
                                sceneChanges = await _videoProcessorService.DetectScenesAsync(item.FilePath!, ffmpegThreshold, token);
                            }
                            catch (Exception ex) { _logger.LogError($"Scene detection failed for {item.FileName}", ex); item.Status = "Error (Scenes)"; errorsEncountered++; videosProcessed++; continue; }

                            if (sceneChanges == null || sceneChanges.Count < 2) // Corrected <
                            { _logger.LogWarning($"Scene detection yielded insufficient results for {item.FileName}. Falling back to random clips."); sceneChanges = null; }
                            else { _logger.LogInfo($"Detected {sceneChanges.Count - 1} potential scenes for {item.FileName}."); }
                        }

                        List<(TimeSpan Start, TimeSpan Duration)> clipsToExtract = new List<(TimeSpan, TimeSpan)>(); // Corrected <>
                        if (sceneChanges != null)
                        {
                            item.Status = "Selecting Scenes...";
                            for (int i = 0; i < sceneChanges.Count - 1; i++) // Corrected <
                            {
                                double sceneStart = sceneChanges[i].TimestampSeconds;
                                double sceneEnd = sceneChanges[i + 1].TimestampSeconds;
                                double sceneDuration = sceneEnd - sceneStart;
                                if (sceneDuration >= MinClipLength && sceneDuration <= MaxClipLength) { clipsToExtract.Add((TimeSpan.FromSeconds(sceneStart), TimeSpan.FromSeconds(sceneDuration))); } // Corrected &&, <=
                                else if (sceneDuration > MaxClipLength) // Corrected >
                                {
                                    double clipDurationSec = random.Next(MinClipLength, MaxClipLength + 1);
                                    if (sceneDuration >= clipDurationSec) // Corrected >=
                                    {
                                        double maxStartOffset = sceneDuration - clipDurationSec;
                                        double startOffset = random.NextDouble() * maxStartOffset;
                                        clipsToExtract.Add((TimeSpan.FromSeconds(sceneStart + startOffset), TimeSpan.FromSeconds(clipDurationSec)));
                                    }
                                }
                            }
                            clipsToExtract = clipsToExtract.OrderBy(x => random.Next()).Take(ClipCount).ToList(); // Corrected =>
                            _logger.LogDebug($"Selected {clipsToExtract.Count} clips based on scene detection for {item.FileName}.");
                        }
                        else
                        {
                            item.Status = "Selecting Random...";
                            for (int i = 0; i < ClipCount; i++) // Corrected <
                            {
                                double maxPossibleStart = Math.Max(0, totalSeconds - MinClipLength);
                                if (maxPossibleStart <= 0) break; // Corrected <=
                                double clipDurationSec = random.Next(MinClipLength, MaxClipLength + 1);
                                double maxStartForThisDuration = Math.Max(0, totalSeconds - clipDurationSec);
                                double startSec = random.NextDouble() * maxStartForThisDuration;
                                clipsToExtract.Add((TimeSpan.FromSeconds(startSec), TimeSpan.FromSeconds(clipDurationSec)));
                            }
                             _logger.LogDebug($"Selected {clipsToExtract.Count} random clips for {item.FileName}.");
                        }

                        for (int i = 0; i < clipsToExtract.Count; i++) // Corrected <
                        {
                             if (token.IsCancellationRequested) break;
                             clipsAttemptedSoFar++;
                             var (clipStart, clipDuration) = clipsToExtract[i];
                             string originalFileName = Path.GetFileNameWithoutExtension(item.FileName!);
                             string outputFileName = $"{originalFileName}_clip_{clipsMadeForThisVideo + 1}.mp4";
                             // Use the validated/created outputBasePath here
                             string initialOutputFullPath = Path.Combine(outputBasePath!, outputFileName);
                             _logger.LogDebug($"Attempting clip {i + 1}/{clipsToExtract.Count} for {item.FileName}: Start={clipStart}, Duration={clipDuration}, Output={initialOutputFullPath}");
                             item.Status = $"Clipping {i + 1}/{clipsToExtract.Count}...";
                             var clipProgress = new Progress<VideoProcessingProgress>(clipReport => { // Corrected =>
                                 double overallPercentage = totalClipsToAttempt > 0 ? ((double)clipsAttemptedSoFar - 1 + clipReport.Percentage) / totalClipsToAttempt : 0; // Corrected >
                                 _progressReporter?.Report(new VideoProcessingProgress { Percentage = overallPercentage, Message = $"Clipping {item.FileName} ({i + 1}/{clipsToExtract.Count}) {clipReport.Percentage:P0}", EstimatedRemaining = clipReport.EstimatedRemaining, CurrentItemIndex = videoIndex, TotalItems = totalVideos }); // Use injected _progressReporter
                             });

                             try
                             {
                                 await _videoProcessorService.ExtractClipAsync(item.FilePath!, initialOutputFullPath, clipStart, clipDuration, FormatAsVertical, RemoveAudio, MirrorVideo, EnhanceVideo, clipProgress, token);
                                 string finalClipPath = initialOutputFullPath;
                                 if (organizeOutput)
                                 {
                                     item.Status = $"Organizing Clip {i + 1}...";
                                     // Use the validated/created outputBasePath here too
                                     try { finalClipPath = await _fileOrganizerService.OrganizeOutputFileAsync(initialOutputFullPath, item.FilePath!, outputBasePath!, "clips"); _logger.LogInfo($"Clip {i + 1} for {item.FileName} organized to {finalClipPath}"); }
                                     catch (Exception orgEx) { _logger.LogError($"Failed to organize clip {i + 1} for {item.FileName}. File remains at '{initialOutputFullPath}'. Error: {orgEx.Message}", orgEx); item.Status = $"Error (Organize {i + 1})"; errorsEncountered++; }
                                 } else { _logger.LogInfo($"Clip {i + 1} for {item.FileName} created at {finalClipPath} (organization disabled)."); }
                                 clipsMadeForThisVideo++; clipsGenerated++;
                             }
                             catch (OperationCanceledException) { _logger.LogInfo($"Clip extraction cancelled for {item.FileName}."); item.Status = "Cancelled"; break; }
                             catch (Exception ex) { _logger.LogError($"Failed to extract clip {i + 1} for {item.FileName}", ex); item.Status = $"Error (Clip {i + 1})"; errorsEncountered++; }
                        }

                        if (token.IsCancellationRequested) { item.Status = "Cancelled"; }
                        else if (item.Status.StartsWith("Clipping") || item.Status.StartsWith("Selecting") || item.Status == "Processing..." || item.Status.StartsWith("Organizing") || item.Status.StartsWith("Error (Organize")) // Corrected ||
                        { item.Status = clipsMadeForThisVideo > 0 ? $"Done ({clipsMadeForThisVideo} clips)" : "Done (No clips)"; } // Corrected >
                        videosProcessed++;
                    }
                }
                catch (Exception ex) { _logger.LogCritical("Unhandled error during clipping process loop.", ex); _dialogService.ShowErrorDialog($"An unexpected error occurred during clipping:\n{ex.Message}", "Processing Error"); errorsEncountered++; }
                finally
                {
                    IsProcessing = false; _clippingCts?.Dispose(); _clippingCts = null;
                    StartStopClippingCommand.NotifyCanExecuteChanged(); ClearQueueCommand.NotifyCanExecuteChanged();
                    string summary = token.IsCancellationRequested ? "Cancelled" : "Finished";
                    _logger.LogInfo($"Clipping process {summary}. Videos Processed: {videosProcessed}/{totalVideos}, Clips Generated: {clipsGenerated}, Errors: {errorsEncountered}.");
                    _progressReporter?.Report(new VideoProcessingProgress { Percentage = 1.0, Message = $"Clipping {summary}." }); // Use injected _progressReporter
                    string finalMessage = $"Clipping {summary}!\n\nVideos Processed: {videosProcessed}/{totalVideos}\nClips Generated: {clipsGenerated}\nErrors: {errorsEncountered}";
                    if (errorsEncountered > 0) { _dialogService.ShowWarningDialog(finalMessage, "Clipping Complete with Errors"); } // Corrected >
                    else if (!token.IsCancellationRequested) { _dialogService.ShowInfoDialog(finalMessage, "Clipping Complete"); }
                }
            }
        }
        private bool CanStartStopClipping() { if (IsProcessing) return true; return VideoQueue.Any() && _videoProcessorService.IsAvailable; } // Corrected &&

        // --- Helper Methods ---
        // --- Made Public Again ---
        public void AddFilesToQueue(string[] filePaths)
        {
            int addedCount = 0;
            foreach (var path in filePaths) { if (File.Exists(path) && !VideoQueue.Any(item => item.FilePath == path)) { VideoQueue.Add(new VideoQueueItem(path)); addedCount++; } } // Corrected &&, =>
            if (addedCount > 0) _logger.LogInfo($"Added {addedCount} file(s) to the queue."); // Corrected >
            UpdateInputDisplayPath(); ClearQueueCommand.NotifyCanExecuteChanged(); StartStopClippingCommand.NotifyCanExecuteChanged();
        }
         // --- Made Public Again ---
         public void AddDirectoryToQueue(string directoryPath)
        {
             if (!Directory.Exists(directoryPath)) return;
             _logger.LogDebug($"Scanning directory for videos: {directoryPath}");
             int addedCount = 0; var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
             try
             {
                 var files = Directory.EnumerateFiles(directoryPath).Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())); // Corrected =>
                 foreach (var file in files) { if (!VideoQueue.Any(item => item.FilePath == file)) { VideoQueue.Add(new VideoQueueItem(file)); addedCount++; } } // Corrected =>
             }
             catch (Exception ex) { _logger.LogError($"Error reading directory {directoryPath}", ex); _dialogService.ShowWarningDialog($"Could not read directory:\n{ex.Message}", "Directory Error"); }
             if (addedCount > 0) _logger.LogInfo($"Added {addedCount} file(s) from directory {directoryPath} to the queue."); // Corrected >
             UpdateInputDisplayPath(); ClearQueueCommand.NotifyCanExecuteChanged(); StartStopClippingCommand.NotifyCanExecuteChanged();
        }
        private void UpdateInputDisplayPath()
        {
            if (!VideoQueue.Any()) { InputDisplayPath = string.Empty; }
            else if (VideoQueue.Count == 1) { InputDisplayPath = VideoQueue[0].FileName; }
            else { InputDisplayPath = $"{VideoQueue.Count} files in queue"; }
            _logger.LogDebug($"Input display path updated: '{InputDisplayPath}'");
        }
    }
}