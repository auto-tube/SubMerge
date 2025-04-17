using System.Windows;
using System.Windows.Controls;
using AutoTubeWpf.ViewModels; // Added for ViewModel reference
using System.Diagnostics; // Added for Debug
using System; // Added for Exception

namespace AutoTubeWpf.Views
{
    /// <summary>
    /// Interaction logic for AiShortView.xaml
    /// </summary>
    public partial class AiShortView : UserControl
    {
        public AiShortView()
        {
            InitializeComponent(); // Assuming build will fix this
        }

        // --- ADDED Click Handler to directly execute command ---
        private async void GenerateAiShortButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[AiShortView.xaml.cs] GenerateAiShortButton_Click entered.");
            // Get the ViewModel from the DataContext
            if (this.DataContext is AiShortViewModel viewModel)
            {
                 Debug.WriteLine("[AiShortView.xaml.cs] ViewModel found.");
                // Check if the command exists and can execute
                // We rely on the CanExecute check within the ViewModel for logging details now
                if (viewModel.GenerateAiShortCommand != null && viewModel.GenerateAiShortCommand.CanExecute(null))
                {
                    Debug.WriteLine("[AiShortView.xaml.cs] Command CanExecute is true. Executing...");
                    try
                    {
                        // Execute the command directly
                        await viewModel.GenerateAiShortCommand.ExecuteAsync(null);
                         Debug.WriteLine("[AiShortView.xaml.cs] Command execution awaited.");
                    }
                    catch (Exception ex)
                    {
                         Debug.WriteLine($"[AiShortView.xaml.cs] Exception during command execution: {ex}");
                         MessageBox.Show($"Error executing command: {ex.Message}", "Command Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                     Debug.WriteLine("[AiShortView.xaml.cs] Command CanExecute is false or command is null.");
                     MessageBox.Show("Command cannot execute (CanExecute returned false or command is null). Check inputs/service status and logs.", "Command Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                     // Manually log the status check details if CanExecute is false
                     viewModel.LogCanGenerateAiShortStatus();
                }
            }
            else
            {
                 Debug.WriteLine("[AiShortView.xaml.cs] DataContext is NOT AiShortViewModel.");
                 MessageBox.Show("DataContext is not the expected AiShortViewModel.", "DataContext Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // --- END ADDED ---
    }
}