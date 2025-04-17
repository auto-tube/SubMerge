// using MaterialDesignThemes.Wpf; // Removed
using System;
using System.Globalization;
using System.Windows; // Added for Visibility
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
    /// Converts a boolean (IsPlayerPlaying) to the appropriate text for the Play/Pause button.
    /// </summary>
    public class BoolToPlayPauseTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                return isPlaying ? "Pause" : "Play";
            }
            return "Play"; // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // One-way conversion
        }
    }

    /// <summary>
    /// Converts between a specific string value (passed as parameter) and a boolean (IsChecked).
    /// Used for binding RadioButtons to a single string property representing the selected option.
    /// </summary>
    public class AlignmentToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Convert from ViewModel string to RadioButton IsChecked (bool)
            // Returns true if the ViewModel's value matches the button's parameter
            string? valueString = value as string;
            string? parameterString = parameter as string;
            return !string.IsNullOrEmpty(valueString) && valueString.Equals(parameterString, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Convert from RadioButton IsChecked (bool) to ViewModel string
            // If this button is checked (value is true), return its parameter (the alignment string)
            // Otherwise, return Binding.DoNothing to let other RadioButtons handle it.
            return (value is true) ? parameter : Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts a null value to Visibility.Collapsed and non-null to Visibility.Visible.
    /// Useful for hiding controls when a related item (like SelectedVideoItem) is null.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // One-way conversion
        }
    }
}