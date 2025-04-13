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
using System.Windows; // For MessageBox - replace with DialogService later

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
        private readonly IConfigurationService _configurationService; // Added config service
        private CancellationTokenSource? _clippingCts; // Added cancellation token source

        // --- UI Bound Properties ---

        [ObservableProperty]
        private string? _inputDisplayPath; // Shows selected file/folder or queue count

        [ObservableProperty]
        private bool _isBatchMode = false; // For file/folder selection dialog type

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
        // Note: Vertical crop/enhance/mirror options from Python GUI are handled during processing, not direct options here yet.
        // For simplicity, let's add a vertical format option directly here for now
        [ObservableProperty]
        private bool _formatAsVertical = true;


        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartStopClippingCommand))]
        [NotifyCanExecuteChangedFor(nameof(ClearQueueCommand))] // Also disable clear while processing
        private bool _isProcessing = false;

        // --- Services ---
        public ClippingViewModel(ILoggerService loggerService, IVideoProcessorService videoProcessorService, IConfigurationService configService) // Added config service
        {
            _logger = loggerService;
            _videoProcessorService = videoProcessorService;
            _configurationService = configService; // Store config service
            _logger.LogInfo("ClippingViewModel initialized.");
            // Load default clipping options from config if needed
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
                    var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
                    {
                        Description = "Select Folder Containing Videos",
                        UseDescriptionForTitle = true
                    };
                    if (dialog.ShowDialog() == true) AddDirectoryToQueue(dialog.SelectedPath);
                }
                else
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Select Video File(s)",
                        Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv|All Files|*.*",
                        Multiselect = true
                    };
                    if (dialog.ShowDialog() == true) AddFilesToQueue(dialog.FileNames);
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError("Error during input selection.", ex);
                 MessageBox.Show($"An error occurred selecting input:\n{ex.Message}", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateInputDisplayPath();
        }

        [RelayCommand(CanExecute = nameof(CanClearQueue))]
        private void ClearQueue()
        {
            _logger.LogInfo("ClearQueue command executed.");
             if (MessageBox.Show($"Are you sure you want to remove all {VideoQueue.Count} video(s) from the queue?",
                                "Confirm Clear Queue", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                VideoQueue.Clear();
                UpdateInputDisplayPath();
                _logger.LogInfo("Video queue cleared.");
            }
        }
        private bool CanClearQueue() => VideoQueue.Any() &amp;&amp; !IsProcessing;


        [RelayCommand(CanExecute = nameof(CanStartStopClipping))]
        private async Task StartStopClippingAsync()
        {
            if (IsProcessing) // --- STOP ---
            {
                _logger.LogInfo("Stop Clipping command executed. Requesting cancellation...");
                try
                {
                    _clippingCts?.Cancel(); // Request cancellation
                }
                catch (ObjectDisposedException) { /* Ignore if already disposed */ }
                catch (Exception ex) { _logger.LogError("Error requesting cancellation.", ex); }
                // UI update (IsProcessing = false) happens in the finally block
            }
            else // --- START ---
            {
                _logger.LogInfo("Start Clipping command executed.");
                _clippingCts = new CancellationTokenSource();
                var token = _clippingCts.Token;
                IsProcessing = true;
                StartStopClippingCommand.NotifyCanExecuteChanged(); // Update button text/state

                // Get output path (essential)
                string? outputBasePath = _configurationService.CurrentSettings.DefaultOutputPath;
                if (string.IsNullOrWhiteSpace(outputBasePath) || !Directory.Exists(outputBasePath))
                {
                    _logger.LogError("Cannot start clipping: Default output path is invalid or not set.");
                    MessageBox.Show("Default Output Folder is not set or invalid. Please configure it in Settings.", "Output Path Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    IsProcessing = false; // Reset state
                    return;
                }

                var queueCopy = VideoQueue.ToList(); // Process a copy
                int totalVideos = queueCopy.Count;
                int videosProcessed = 0;
                int clipsGenerated = 0;
                int errorsEncountered = 0;
                var random = new Random();

                // TODO: Pass progress reporter to update MainWindowViewModel progress bar
                // var progressReporter = new Progress<double>(percent => MainWindowViewModel.ProgressValue = percent);

                try
                {
                    foreach (var item in queueCopy)
                    {
                        if (token.IsCancellationRequested)
                        {
                            _logger.LogInfo("Clipping process cancelled by user.");
                            item.Status = "Cancelled";
                            break; // Exit the loop
                        }

                        _logger.LogInfo($"Processing video: {item.FileName}");
                        item.Status = "Processing...";

                        TimeSpan? duration = null;
                        try
                        {
                            duration = await _videoProcessorService.GetVideoDurationAsync(item.FilePath!, token);
                        }
                        catch (Exception ex)
                        {
                             _logger.LogError($"Failed to get duration for {item.FileName}", ex);
                             item.Status = "Error (Duration)";
                             errorsEncountered++;
                             continue; // Skip to next video
                        }

                        if (duration == null || duration.Value.TotalSeconds <= MinClipLength)
                        {
                            _logger.LogWarning($"Skipping {item.FileName}: Duration ({duration?.TotalSeconds ?? 0}s) is too short or could not be determined.");
                            item.Status = "Skipped (Short)";
                            continue; // Skip to next video
                        }

                        item.Duration = duration; // Update duration in UI
                        double totalSeconds = duration.Value.TotalSeconds;
                        int clipsMadeForThisVideo = 0;

                        for (int i = 0; i < ClipCount; i++)
                        {
                             if (token.IsCancellationRequested) break; // Check cancellation before each clip

                             // --- Calculate Clip Start/Duration ---
                             // Ensure clip doesn't exceed video bounds and respects min/max length
                             double maxPossibleStart = Math.Max(0, totalSeconds - MinClipLength);
                             if (maxPossibleStart <= 0) break; // Cannot make more clips

                             double clipDurationSec = random.Next(MinClipLength, MaxClipLength + 1);
                             double maxStartForThisDuration = Math.Max(0, totalSeconds - clipDurationSec);
                             double startSec = random.NextDouble() * maxStartForThisDuration; // Random start within valid range

                             TimeSpan clipStart = TimeSpan.FromSeconds(startSec);
                             TimeSpan clipDuration = TimeSpan.FromSeconds(clipDurationSec);

                             // --- Generate Output Path ---
                             string originalFileName = Path.GetFileNameWithoutExtension(item.FileName!);
                             string outputDir = outputBasePath; // TODO: Add file organization later
                             string outputFileName = $"{originalFileName}_clip_{clipsMadeForThisVideo + 1}.mp4";
                             string outputFullPath = Path.Combine(outputDir, outputFileName);

                             _logger.LogDebug($"Attempting clip {i + 1}/{ClipCount} for {item.FileName}: Start={clipStart}, Duration={clipDuration}, Output={outputFullPath}");
                             item.Status = $"Clipping {i + 1}/{ClipCount}...";

                             try
                             {
                                 // TODO: Pass actual progress reporter if implemented in ExtractClipAsync
                                 await _videoProcessorService.ExtractClipAsync(item.FilePath!, outputFullPath, clipStart, clipDuration, FormatAsVertical, null, token);
                                 clipsMadeForThisVideo++;
                                 clipsGenerated++;
                             }
                             catch (OperationCanceledException)
                             {
                                 _logger.LogInfo($"Clip extraction cancelled for {item.FileName}.");
                                 item.Status = "Cancelled";
                                 break; // Exit inner loop for this video
                             }
                             catch (Exception ex)
                             {
                                 _logger.LogError($"Failed to extract clip {i + 1} for {item.FileName}", ex);
                                 item.Status = $"Error (Clip {i + 1})";
                                 errorsEncountered++;
                                 // Decide whether to continue with next clip for this video or skip?
                                 // Let's continue trying other clips for this video for now.
                             }
                        } // End clip loop

                        if (token.IsCancellationRequested)
                        {
                             item.Status = "Cancelled";
                        }
                        else if (item.Status.StartsWith("Clipping") || item.Status == "Processing...") // If loop finished without error/cancel
                        {
                             item.Status = clipsMadeForThisVideo > 0 ? $"Done ({clipsMadeForThisVideo} clips)" : "Done (No clips)";
                        }

                        videosProcessed++;
                        // TODO: Report progress: (double)videosProcessed / totalVideos

                    } // End video loop
                }
                catch (Exception ex) // Catch unexpected errors during the loop
                {
                     _logger.LogCritical("Unhandled error during clipping process loop.", ex);
                     MessageBox.Show($"An unexpected error occurred during clipping:\n{ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                     errorsEncountered++;
                }
                finally
                {
                    IsProcessing = false; // Ensure state is reset
                    _clippingCts?.Dispose(); // Dispose CTS
                    _clippingCts = null;
                    StartStopClippingCommand.NotifyCanExecuteChanged(); // Update button state/text
                    ClearQueueCommand.NotifyCanExecuteChanged(); // Re-enable clear button if needed

                    // Final status message
                    string summary = token.IsCancellationRequested ? "Cancelled" : "Finished";
                    _logger.LogInfo($"Clipping process {summary}. Videos Processed: {videosProcessed}/{totalVideos}, Clips Generated: {clipsGenerated}, Errors: {errorsEncountered}.");
                    // TODO: Update main status bar
                    MessageBox.Show($"Clipping {summary}!\n\nVideos Processed: {videosProcessed}/{totalVideos}\nClips Generated: {clipsGenerated}\nErrors: {errorsEncountered}",
                                    "Clipping Complete", MessageBoxButton.OK, errorsEncountered > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                    // Optionally clear queue on successful completion?
                    // if (!token.IsCancellationRequested && errorsEncountered == 0) VideoQueue.Clear(); UpdateInputDisplayPath();
                }
            } // End START block
        }
        private bool CanStartStopClipping()
        {
            if (IsProcessing) return true; // Can always stop
            return VideoQueue.Any() &amp;&amp; _videoProcessorService.IsAvailable; // Can start if queue has items and ffmpeg is ready
        }


        // --- Helper Methods ---

        public void AddFilesToQueue(string[] filePaths) // Made public for potential drop handler
        {
            int addedCount = 0;
            foreach (var path in filePaths)
            {
                if (File.Exists(path) &amp;&amp; !VideoQueue.Any(item => item.FilePath == path))
                {
                    VideoQueue.Add(new VideoQueueItem(path));
                    addedCount++;
                }
            }
             if (addedCount > 0) _logger.LogInfo($"Added {addedCount} file(s) to the queue.");
             UpdateInputDisplayPath();
             ClearQueueCommand.NotifyCanExecuteChanged(); // Manually trigger CanExecute update
             StartStopClippingCommand.NotifyCanExecuteChanged();
        }

         public void AddDirectoryToQueue(string directoryPath) // Made public for potential drop handler
        {
             if (!Directory.Exists(directoryPath)) return;
             _logger.LogDebug($"Scanning directory for videos: {directoryPath}");
             int addedCount = 0;
             var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
             try
             {
                 var files = Directory.EnumerateFiles(directoryPath)
                                     .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                 foreach (var file in files)
                 {
                     if (!VideoQueue.Any(item => item.FilePath == file))
                     {
                         VideoQueue.Add(new VideoQueueItem(file));
                         addedCount++;
                     }
                 }
             }
             catch (Exception ex)
             {
                  _logger.LogError($"Error reading directory {directoryPath}", ex);
                  MessageBox.Show($"Could not read directory:\n{ex.Message}", "Directory Error", MessageBoxButton.OK, MessageBoxImage.Warning);
             }
             if (addedCount > 0) _logger.LogInfo($"Added {addedCount} file(s) from directory {directoryPath} to the queue.");
             UpdateInputDisplayPath();
             ClearQueueCommand.NotifyCanExecuteChanged();
             StartStopClippingCommand.NotifyCanExecuteChanged();
        }


        private void UpdateInputDisplayPath()
        {
            if (!VideoQueue.Any())
            {
                InputDisplayPath = string.Empty;
            }
            else if (VideoQueue.Count == 1)
            {
                InputDisplayPath = VideoQueue[0].FileName;
            }
            else
            {
                InputDisplayPath = $"{VideoQueue.Count} files in queue";
            }
             _logger.LogDebug($"Input display path updated: '{InputDisplayPath}'");
        }
    }
}