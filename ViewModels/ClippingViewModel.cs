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
        private readonly IProgress<VideoProcessingProgress>? _progressReporter;
        private CancellationTokenSource? _clippingCts;
        private DispatcherTimer? _playerTimer; // Timer to update slider position
        private bool _isSeeking = false; // Flag to prevent slider updates during drag

        // --- UI Bound Properties ---

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
            IProgress<VideoProcessingProgress>? progressReporter = null)
        {
            _logger = loggerService;
            _videoProcessorService = videoProcessorService;
            _configurationService = configService;
            _fileOrganizerService = fileOrganizerService;
            _dialogService = dialogService;
            _progressReporter = progressReporter;
            _logger.LogInfo("ClippingViewModel initialized.");

            // Initialize player timer
            InitializePlayerTimer();
        }

        // --- Partial Property Changed Methods ---
        partial void OnSelectedVideoItemChanged(VideoQueueItem? value)
        {
            _logger.LogDebug($"SelectedVideoItem changed: {value?.FileName ?? "null"}");
            // Stop previous playback and timer
            IsPlayerPlaying = false;
            _playerTimer?.Stop();
            PlayerPositionSeconds = 0;
            PlayerDurationSeconds = 1.0; // Reset duration

            if (value?.FilePath != null &amp;&amp; File.Exists(value.FilePath))
            {
                try
                {
                    PlayerSource = new Uri(value.FilePath);
                    _logger.LogInfo($"Player source set to: {PlayerSource}");
                    // Duration will be updated in MediaOpened event handler in code-behind
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

        // --- Player Timer ---
        private void InitializePlayerTimer()
        {
            _playerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // Update slider roughly 5 times per second
            };
            _playerTimer.Tick += PlayerTimer_Tick;
        }

        private void PlayerTimer_Tick(object? sender, EventArgs e)
        {
            // Update slider only if user is not dragging it
            if (!_isSeeking &amp;&amp; IsPlayerPlaying)
            {
                // This needs to get the actual position from the MediaElement in code-behind
                // For now, we just simulate progress for testing ViewModel logic
                // PlayerPositionSeconds += 0.2 * PlayerSpeedRatio;
                // if (PlayerPositionSeconds >= PlayerDurationSeconds) StopPlayer();
            }
        }

        // --- Commands ---

        [RelayCommand]
        private void SelectInput()
        {
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
                StopPlayer(); // Stop playback if clearing queue
                VideoQueue.Clear();
                SelectedVideoItem = null; // Clear selection
                UpdateInputDisplayPath();
                _logger.LogInfo("Video queue cleared.");
            }
        }
        private bool CanClearQueue() => VideoQueue.Any() &amp;&amp; !IsProcessing;

        [RelayCommand(CanExecute = nameof(CanPlayPause))]
        private void PlayPause()
        {
            IsPlayerPlaying = !IsPlayerPlaying; // Toggle state
            if (IsPlayerPlaying) _playerTimer?.Start();
            else _playerTimer?.Stop();
            // Actual Play/Pause action happens in code-behind via binding or event
            _logger.LogDebug($"Play/Pause command executed. IsPlaying: {IsPlayerPlaying}");
        }
        private bool CanPlayPause() => SelectedVideoItem != null;

        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            StopPlayer();
            // Actual Stop action happens in code-behind via binding or event
             _logger.LogDebug("Stop command executed.");
        }
        private bool CanStop() => SelectedVideoItem != null;

        private void StopPlayer()
        {
             IsPlayerPlaying = false;
             _playerTimer?.Stop();
             PlayerPositionSeconds = 0; // Reset position
             // Need to signal MediaElement in code-behind to stop and reset position
        }

        // Called from code-behind when slider drag starts
        public void StartSeek() { _isSeeking = true; _playerTimer?.Stop(); }
        // Called from code-behind when slider drag ends
        public void EndSeek() { _isSeeking = false; if (IsPlayerPlaying) _playerTimer?.Start(); }
        // Called from code-behind when MediaElement opens
        public void SetMediaDuration(TimeSpan duration) { PlayerDurationSeconds = duration.TotalSeconds; }
        // Called from code-behind to update position from MediaElement
        public void UpdatePlayerPosition(TimeSpan position) { if (!_isSeeking) PlayerPositionSeconds = position.TotalSeconds; }
        // Called from code-behind when media ends
        public void MediaPlaybackEnded() { StopPlayer(); }


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

                if (string.IsNullOrWhiteSpace(outputBasePath) || !Directory.Exists(outputBasePath))
                { _logger.LogError("Cannot start clipping: Default output path is invalid or not set."); _dialogService.ShowErrorDialog("Default Output Folder is not set or invalid. Please configure it in Settings.", "Output Path Error"); IsProcessing = false; return; }

                var queueCopy = VideoQueue.ToList();
                int totalVideos = queueCopy.Count;
                int videosProcessed = 0;
                int clipsGenerated = 0;
                int errorsEncountered = 0;
                var random = new Random();
                int totalClipsToAttempt = queueCopy.Sum(item => ClipCount);
                int clipsAttemptedSoFar = 0;

                try
                {
                    for(int videoIndex = 0; videoIndex < totalVideos; videoIndex++)
                    {
                        var item = queueCopy[videoIndex];
                        if (token.IsCancellationRequested) { _logger.LogInfo("Clipping process cancelled by user."); item.Status = "Cancelled"; break; }

                        _logger.LogInfo($"Processing video {videoIndex + 1}/{totalVideos}: {item.FileName}");
                        item.Status = "Getting Duration...";
                        _progressReporter?.Report(new VideoProcessingProgress { Percentage = (double)videosProcessed / totalVideos, Message = $"Processing video {videoIndex + 1}/{totalVideos}...", CurrentItemIndex = videoIndex, TotalItems = totalVideos });

                        TimeSpan? duration = null;
                        try { duration = await _videoProcessorService.GetVideoDurationAsync(item.FilePath!, token); }
                        catch (Exception ex) { _logger.LogError($"Failed to get duration for {item.FileName}", ex); item.Status = "Error (Duration)"; errorsEncountered++; videosProcessed++; continue; }

                        if (duration == null || duration.Value.TotalSeconds <= MinClipLength)
                        { _logger.LogWarning($"Skipping {item.FileName}: Duration ({duration?.TotalSeconds ?? 0}s) is too short or could not be determined."); item.Status = "Skipped (Short)"; videosProcessed++; continue; }

                        item.Duration = duration;
                        double totalSeconds = duration.Value.TotalSeconds;
                        int clipsMadeForThisVideo = 0;
                        List<SceneChangeInfo>? sceneChanges = null;

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

                            if (sceneChanges == null || sceneChanges.Count < 2)
                            { _logger.LogWarning($"Scene detection yielded insufficient results for {item.FileName}. Falling back to random clips."); sceneChanges = null; }
                            else { _logger.LogInfo($"Detected {sceneChanges.Count - 1} potential scenes for {item.FileName}."); }
                        }

                        List<(TimeSpan Start, TimeSpan Duration)> clipsToExtract = new List<(TimeSpan, TimeSpan)>();
                        if (sceneChanges != null)
                        {
                            item.Status = "Selecting Scenes...";
                            for (int i = 0; i < sceneChanges.Count - 1; i++)
                            {
                                double sceneStart = sceneChanges[i].TimestampSeconds;
                                double sceneEnd = sceneChanges[i + 1].TimestampSeconds;
                                double sceneDuration = sceneEnd - sceneStart;
                                if (sceneDuration >= MinClipLength &amp;&amp; sceneDuration <= MaxClipLength) { clipsToExtract.Add((TimeSpan.FromSeconds(sceneStart), TimeSpan.FromSeconds(sceneDuration))); }
                                else if (sceneDuration > MaxClipLength)
                                {
                                    double clipDurationSec = random.Next(MinClipLength, MaxClipLength + 1);
                                    if (sceneDuration >= clipDurationSec)
                                    {
                                        double maxStartOffset = sceneDuration - clipDurationSec;
                                        double startOffset = random.NextDouble() * maxStartOffset;
                                        clipsToExtract.Add((TimeSpan.FromSeconds(sceneStart + startOffset), TimeSpan.FromSeconds(clipDurationSec)));
                                    }
                                }
                            }
                            clipsToExtract = clipsToExtract.OrderBy(x => random.Next()).Take(ClipCount).ToList();
                            _logger.LogDebug($"Selected {clipsToExtract.Count} clips based on scene detection for {item.FileName}.");
                        }
                        else
                        {
                            item.Status = "Selecting Random...";
                            for (int i = 0; i < ClipCount; i++)
                            {
                                double maxPossibleStart = Math.Max(0, totalSeconds - MinClipLength);
                                if (maxPossibleStart <= 0) break;
                                double clipDurationSec = random.Next(MinClipLength, MaxClipLength + 1);
                                double maxStartForThisDuration = Math.Max(0, totalSeconds - clipDurationSec);
                                double startSec = random.NextDouble() * maxStartForThisDuration;
                                clipsToExtract.Add((TimeSpan.FromSeconds(startSec), TimeSpan.FromSeconds(clipDurationSec)));
                            }
                             _logger.LogDebug($"Selected {clipsToExtract.Count} random clips for {item.FileName}.");
                        }

                        for (int i = 0; i < clipsToExtract.Count; i++)
                        {
                             if (token.IsCancellationRequested) break;
                             clipsAttemptedSoFar++;
                             var (clipStart, clipDuration) = clipsToExtract[i];
                             string originalFileName = Path.GetFileNameWithoutExtension(item.FileName!);
                             string outputFileName = $"{originalFileName}_clip_{clipsMadeForThisVideo + 1}.mp4";
                             string initialOutputFullPath = Path.Combine(outputBasePath!, outputFileName);
                             _logger.LogDebug($"Attempting clip {i + 1}/{clipsToExtract.Count} for {item.FileName}: Start={clipStart}, Duration={clipDuration}, Output={initialOutputFullPath}");
                             item.Status = $"Clipping {i + 1}/{clipsToExtract.Count}...";
                             var clipProgress = new Progress<VideoProcessingProgress>(clipReport => {
                                 double overallPercentage = totalClipsToAttempt > 0 ? ((double)clipsAttemptedSoFar - 1 + clipReport.Percentage) / totalClipsToAttempt : 0;
                                 _progressReporter?.Report(new VideoProcessingProgress { Percentage = overallPercentage, Message = $"Clipping {item.FileName} ({i + 1}/{clipsToExtract.Count}) {clipReport.Percentage:P0}", EstimatedRemaining = clipReport.EstimatedRemaining, CurrentItemIndex = videoIndex, TotalItems = totalVideos });
                             });

                             try
                             {
                                 await _videoProcessorService.ExtractClipAsync(item.FilePath!, initialOutputFullPath, clipStart, clipDuration, FormatAsVertical, RemoveAudio, MirrorVideo, EnhanceVideo, clipProgress, token);
                                 string finalClipPath = initialOutputFullPath;
                                 if (organizeOutput)
                                 {
                                     item.Status = $"Organizing Clip {i + 1}...";
                                     try { finalClipPath = await _fileOrganizerService.OrganizeOutputFileAsync(initialOutputFullPath, item.FilePath!, outputBasePath!, "clips"); _logger.LogInfo($"Clip {i + 1} for {item.FileName} organized to {finalClipPath}"); }
                                     catch (Exception orgEx) { _logger.LogError($"Failed to organize clip {i + 1} for {item.FileName}. File remains at '{initialOutputFullPath}'. Error: {orgEx.Message}", orgEx); item.Status = $"Error (Organize {i + 1})"; errorsEncountered++; }
                                 } else { _logger.LogInfo($"Clip {i + 1} for {item.FileName} created at {finalClipPath} (organization disabled)."); }
                                 clipsMadeForThisVideo++; clipsGenerated++;
                             }
                             catch (OperationCanceledException) { _logger.LogInfo($"Clip extraction cancelled for {item.FileName}."); item.Status = "Cancelled"; break; }
                             catch (Exception ex) { _logger.LogError($"Failed to extract clip {i + 1} for {item.FileName}", ex); item.Status = $"Error (Clip {i + 1})"; errorsEncountered++; }
                        }

                        if (token.IsCancellationRequested) { item.Status = "Cancelled"; }
                        else if (item.Status.StartsWith("Clipping") || item.Status.StartsWith("Selecting") || item.Status == "Processing..." || item.Status.StartsWith("Organizing") || item.Status.StartsWith("Error (Organize"))
                        { item.Status = clipsMadeForThisVideo > 0 ? $"Done ({clipsMadeForThisVideo} clips)" : "Done (No clips)"; }
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
                    _progressReporter?.Report(new VideoProcessingProgress { Percentage = 1.0, Message = $"Clipping {summary}." });
                    string finalMessage = $"Clipping {summary}!\n\nVideos Processed: {videosProcessed}/{totalVideos}\nClips Generated: {clipsGenerated}\nErrors: {errorsEncountered}";
                    if (errorsEncountered > 0) { _dialogService.ShowWarningDialog(finalMessage, "Clipping Complete with Errors"); }
                    else if (!token.IsCancellationRequested) { _dialogService.ShowInfoDialog(finalMessage, "Clipping Complete"); }
                }
            }
        }
        private bool CanStartStopClipping() { if (IsProcessing) return true; return VideoQueue.Any() &amp;&amp; _videoProcessorService.IsAvailable; }

        // --- Helper Methods ---
        public void AddFilesToQueue(string[] filePaths)
        {
            int addedCount = 0;
            foreach (var path in filePaths) { if (File.Exists(path) &amp;&amp; !VideoQueue.Any(item => item.FilePath == path)) { VideoQueue.Add(new VideoQueueItem(path)); addedCount++; } }
            if (addedCount > 0) _logger.LogInfo($"Added {addedCount} file(s) to the queue.");
            UpdateInputDisplayPath(); ClearQueueCommand.NotifyCanExecuteChanged(); StartStopClippingCommand.NotifyCanExecuteChanged();
        }
         public void AddDirectoryToQueue(string directoryPath)
        {
             if (!Directory.Exists(directoryPath)) return;
             _logger.LogDebug($"Scanning directory for videos: {directoryPath}");
             int addedCount = 0; var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
             try
             {
                 var files = Directory.EnumerateFiles(directoryPath).Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                 foreach (var file in files) { if (!VideoQueue.Any(item => item.FilePath == file)) { VideoQueue.Add(new VideoQueueItem(file)); addedCount++; } }
             }
             catch (Exception ex) { _logger.LogError($"Error reading directory {directoryPath}", ex); _dialogService.ShowWarningDialog($"Could not read directory:\n{ex.Message}", "Directory Error"); }
             if (addedCount > 0) _logger.LogInfo($"Added {addedCount} file(s) from directory {directoryPath} to the queue.");
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