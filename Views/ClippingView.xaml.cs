using AutoTubeWpf.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For Thumb
using System.Windows.Threading; // For DispatcherTimer

namespace AutoTubeWpf.Views
{
    /// <summary>
    /// Interaction logic for ClippingView.xaml
    /// </summary>
    public partial class ClippingView : UserControl
    {
        private DispatcherTimer? _timer; // Timer for updating slider position

        public ClippingView()
        {
            InitializeComponent();
            // Initialize timer here or in Loaded event
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += Timer_Tick;
        }

        // Helper to safely get the ViewModel
        private ClippingViewModel? GetViewModel() => DataContext as ClippingViewModel;

        private void ClippingView_Drop(object sender, DragEventArgs e)
        {
            if (GetViewModel() is ClippingViewModel viewModel)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[]? droppedItems = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (droppedItems != null &amp;&amp; droppedItems.Length > 0)
                    {
                        viewModel.Logger?.LogDebug($"Drop event detected with {droppedItems.Length} item(s).");
                        var filesToAdd = droppedItems.Where(File.Exists).ToArray();
                        var directoriesToAdd = droppedItems.Where(Directory.Exists).ToArray();
                        if (filesToAdd.Length > 0) viewModel.AddFilesToQueue(filesToAdd);
                        foreach (var dir in directoriesToAdd) viewModel.AddDirectoryToQueue(dir);
                    }
                    else { viewModel.Logger?.LogDebug("Drop event occurred but no valid file/folder data found."); }
                }
                 else { viewModel.Logger?.LogDebug("Drop event occurred but data format was not FileDrop."); }
            }
             else { System.Diagnostics.Debug.WriteLine("[ClippingView_Drop] Error: DataContext is not a ClippingViewModel."); }
            e.Handled = true;
        }

        // --- MediaElement Event Handlers ---

        private void PreviewPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (GetViewModel() is ClippingViewModel viewModel &amp;&amp; PreviewPlayer.NaturalDuration.HasTimeSpan)
            {
                viewModel.SetMediaDuration(PreviewPlayer.NaturalDuration.TimeSpan);
                viewModel.Logger?.LogDebug($"Media opened. Duration: {PreviewPlayer.NaturalDuration.TimeSpan}");
            }
        }

        private void PreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            GetViewModel()?.MediaPlaybackEnded(); // Signal ViewModel that playback stopped
            viewModel.Logger?.LogDebug("Media ended.");
        }

        private void PreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            GetViewModel()?.StopPlayer(); // Stop on failure
            GetViewModel()?.Logger?.LogError($"Media failed: {e.ErrorException.Message}", e.ErrorException);
            GetViewModel()?.DialogService?.ShowErrorDialog($"Could not play the selected video.\nError: {e.ErrorException.Message}", "Playback Error");
        }

        // --- Playback Control Event Handlers (Code-behind interaction with MediaElement) ---

        // Timer tick to update ViewModel's position property from MediaElement
        private void Timer_Tick(object? sender, EventArgs e)
        {
             if (GetViewModel() is ClippingViewModel viewModel &amp;&amp; PreviewPlayer.Source != null &amp;&amp; PreviewPlayer.NaturalDuration.HasTimeSpan)
             {
                 viewModel.UpdatePlayerPosition(PreviewPlayer.Position);
             }
        }

        // React to ViewModel's IsPlayerPlaying property changes
        // This requires modifying the ViewModel property slightly or using a different approach like messaging.
        // For simplicity now, we'll handle Play/Pause/Stop directly in command handlers if possible,
        // or rely on the ViewModel calling methods here if direct control is needed.
        // Let's assume commands in ViewModel directly control IsPlayerPlaying and timer state,
        // and we use code-behind primarily for MediaElement interaction.

        // Example: If PlayPauseCommand toggles IsPlayerPlaying, we might observe that property
        // or have the command call methods here. Let's add methods called by ViewModel commands.

        public void PlayMedia()
        {
            PreviewPlayer.Play();
            _timer?.Start();
            GetViewModel()?.Logger?.LogDebug("Media playing.");
        }

        public void PauseMedia()
        {
            PreviewPlayer.Pause();
            _timer?.Stop();
             GetViewModel()?.Logger?.LogDebug("Media paused.");
       }

        public void StopMedia()
        {
            PreviewPlayer.Stop(); // Stops and resets position
            _timer?.Stop();
            GetViewModel()?.UpdatePlayerPosition(TimeSpan.Zero); // Ensure ViewModel position resets
             GetViewModel()?.Logger?.LogDebug("Media stopped.");
       }

        public void SetMediaPosition(TimeSpan position)
        {
             PreviewPlayer.Position = position;
             GetViewModel()?.Logger?.LogDebug($"Media position set to: {position}");
        }

         public void SetSpeedRatio(double speedRatio)
        {
             PreviewPlayer.SpeedRatio = speedRatio;
             GetViewModel()?.Logger?.LogDebug($"Media speed ratio set to: {speedRatio}");
        }


        // --- Slider Event Handlers ---

        private void SeekSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            GetViewModel()?.StartSeek();
        }

        private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (GetViewModel() is ClippingViewModel viewModel &amp;&amp; sender is Slider slider)
            {
                var newPosition = TimeSpan.FromSeconds(slider.Value);
                SetMediaPosition(newPosition); // Update MediaElement position
                viewModel.EndSeek();
            }
        }

        // --- DataGrid Selection Change ---
        private void VideoQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel's SelectedVideoItem property is already bound TwoWay.
            // This handler is primarily for triggering actions if needed,
            // but the ViewModel handles loading the source via OnSelectedVideoItemChanged.
             if (GetViewModel() is ClippingViewModel viewModel)
             {
                  viewModel.Logger?.LogDebug("VideoQueue selection changed.");
                  // Optionally stop playback when selection changes?
                  // viewModel.StopPlayer(); // Call the VM method to handle state
             }
        }
    }
}