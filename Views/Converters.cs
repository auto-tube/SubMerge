using MaterialDesignThemes.Wpf; // Required for PackIconKind
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AutoTubeWpf.Views // Keep namespace consistent with Views
{
    /// <summary>
    /// Converts a boolean (IsProcessing) to the appropriate text for the Start/Stop button.
    /// </summary>
    public class BoolToProcessingButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isProcessing)
            {
                return isProcessing ? "Stop Clipping" : "Start Clipping Queue";
            }
            return "Start Clipping Queue"; // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // One-way conversion
        }
    }

    /// <summary>
    /// Converts a boolean (IsProcessing) to the appropriate background color for the Start/Stop button.
    /// </summary>
    public class BoolToProcessingButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isProcessing)
            {
                // Using standard WPF Brushes - Consider using MaterialDesign brushes later
                return isProcessing ? Brushes.Red : Brushes.Green;
            }
            return Brushes.Green; // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // One-way conversion
        }
    }

    /// <summary>
    /// Converts a boolean (IsPlayerPlaying) to the appropriate Material Design Icon Kind for Play/Pause.
    /// </summary>
    public class BoolToPlayPauseIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                return isPlaying ? PackIconKind.Pause : PackIconKind.Play;
            }
            return PackIconKind.Play; // Default to Play icon
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // One-way conversion
        }
    }
}