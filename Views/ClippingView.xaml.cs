using AutoTubeWpf.ViewModels;
using System;
using System.ComponentModel; // Added for PropertyChangedEventArgs
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For Thumb
// using System.Windows.Threading; // No longer needed for DispatcherTimer

namespace AutoTubeWpf.Views
{
    /// <summary>
    /// Interaction logic for ClippingView.xaml
    /// </summary>
    public partial class ClippingView : UserControl
    {
        // Timer removed - will be handled by bindings/behaviors

        public ClippingView()
        {
            InitializeComponent();
            DataContextChanged += ClippingView_DataContextChanged;
        }

        private void ClippingView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel if it exists
            if (e.OldValue is ClippingViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Subscribe to new ViewModel if it exists
            if (e.NewValue is ClippingViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
                // Initial sync
                UpdateSpeedRatio(newViewModel.PlayerSpeedRatio);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClippingViewModel.PlayerSpeedRatio) && sender is ClippingViewModel viewModel)
            {
                UpdateSpeedRatio(viewModel.PlayerSpeedRatio);
            }
            // Handle IsPlayerPlaying changes for Play/Pause
            else if (e.PropertyName == nameof(ClippingViewModel.IsPlayerPlaying) && sender is ClippingViewModel vm)
            {
                if (vm.IsPlayerPlaying)
                {
                    PreviewPlayer.Play();
                }
                else
                {
                    PreviewPlayer.Pause(); // Or Stop() if Pause doesn't work as expected
                }
            }
        }

        private void UpdateSpeedRatio(double speedRatio)
        {
            // Ensure we're interacting with the UI element safely
            Dispatcher.Invoke(() =>
            {
                if (PreviewPlayer != null) // Check if player element exists
                {
                    try
                    {
                        PreviewPlayer.SpeedRatio = speedRatio;
                    }
                    catch (Exception ex)
                    {
                        // Log potential errors if setting speed ratio fails
                        System.Diagnostics.Debug.WriteLine($"[ClippingView] Error setting SpeedRatio: {ex.Message}");
                    }
                }
            });
        }


        // Helper to safely get the ViewModel
        private ClippingViewModel? GetViewModel() => DataContext as ClippingViewModel;

        // Keep Drop handler for file/folder input
        private void ClippingView_Drop(object sender, DragEventArgs e)
        {
            if (GetViewModel() is ClippingViewModel viewModel)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[]? droppedItems = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (droppedItems != null && droppedItems.Length > 0) // Fixed &&
                    {
                        // viewModel.Logger?.LogDebug($"Drop event detected with {droppedItems.Length} item(s)."); // Logging removed from View
                        var filesToAdd = droppedItems.Where(File.Exists).ToArray();
                        var directoriesToAdd = droppedItems.Where(Directory.Exists).ToArray();
                        if (filesToAdd.Length > 0)
                        {
                             viewModel.AddFilesToQueue(filesToAdd);
                        }
                        // Removed misplaced else
                        foreach (var dir in directoriesToAdd)
                        {
                             viewModel.AddDirectoryToQueue(dir);
                        }
                    }
                    else // Correctly placed else for if (droppedItems != null && droppedItems.Length > 0)
                    {
                         // viewModel.Logger?.LogDebug("Drop event occurred but no valid file/folder data found."); // Logging removed from View
                    }
                }
                else // Correctly placed else for if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                     // viewModel.Logger?.LogDebug("Drop event occurred but data format was not FileDrop."); // Logging removed from View
                }
            }
            else // Correctly placed else for if (GetViewModel() is ClippingViewModel viewModel)
            {
                 System.Diagnostics.Debug.WriteLine("[ClippingView_Drop] Error: DataContext is not a ClippingViewModel.");
            }
            e.Handled = true;
        }

        // --- MediaElement Event Handlers (Standard WPF) ---
        private void PreviewPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Ensure vm is assigned before checking PreviewPlayer properties
            if (GetViewModel() is ClippingViewModel vm)
            {
                 if (PreviewPlayer != null && PreviewPlayer.NaturalDuration.HasTimeSpan)
                 {
                      vm.MediaOpenedCommand.Execute(PreviewPlayer.NaturalDuration.TimeSpan);
                 }
            }
        }

        private void PreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // TODO: Call ViewModel command, e.g., GetViewModel()?.MediaEndedCommand.Execute(null);
        }

        private void PreviewPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            GetViewModel()?.MediaFailedCommand.Execute(e.ErrorException?.Message ?? "Unknown playback error");
        }

        // --- Playback Control Methods Removed ---
        // ViewModel will now control playback state via bound properties and commands,
        // and Behaviors in XAML will react to these properties to control the MediaElement.

        // --- Slider Event Handlers (Standard WPF) ---
        private void SeekSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            GetViewModel()?.SeekDragStartedCommand.Execute(null);
        }

        private void SeekSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GetViewModel() is ClippingViewModel vm)
            {
                // Update player position AFTER drag completes
                PreviewPlayer.Position = TimeSpan.FromSeconds(SeekSlider.Value);
                // Update ViewModel position explicitly if needed, though TwoWay binding should handle it
                vm.PlayerPositionSeconds = SeekSlider.Value;
                vm.SeekDragCompletedCommand.Execute(null);
            }
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Only update MediaElement if the user is NOT actively dragging the slider thumb
            if (GetViewModel() is ClippingViewModel vm && !vm.IsSeeking)
            {
                 // Update player position based on slider value changes (e.g., from timer)
                 // Check if the change is significant enough to avoid jitter
                 if (Math.Abs(PreviewPlayer.Position.TotalSeconds - e.NewValue) > 0.1)
                 {
                     PreviewPlayer.Position = TimeSpan.FromSeconds(e.NewValue);
                 }
            }
        }


        // --- DataGrid Selection Change ---
        // Keep this for now, although core logic is handled by TwoWay binding.
        private void VideoQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel's SelectedVideoItem property is already bound TwoWay.
            // This handler is primarily for triggering actions if needed,
            // but the ViewModel handles loading the source via OnSelectedVideoItemChanged.
             if (GetViewModel() is ClippingViewModel viewModel)
             {
                  // viewModel.Logger?.LogDebug("VideoQueue selection changed."); // Logging removed from View
                  // Optionally stop playback when selection changes?
                  // viewModel.StopPlayer(); // Call the VM method to handle state
             }
        }
    }
}