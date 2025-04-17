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
            InitializeComponent(); 
        }

        // --- Click Handler for Generate AI Short Button ---
        private async void GenerateAiShortButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[AiShortView.xaml.cs] GenerateAiShortButton_Click entered.");
            // Get the ViewModel from the DataContext
            if (this.DataContext is AiShortViewModel viewModel)
            {
                 Debug.WriteLine("[AiShortView.xaml.cs] ViewModel found for AI Short button.");
                // Check if the command exists and can execute
                if (viewModel.GenerateAiShortCommand != null && viewModel.GenerateAiShortCommand.CanExecute(null)) 
                {
                    Debug.WriteLine("[AiShortView.xaml.cs] AI Short Command CanExecute is true. Executing...");
                    try
                    {
                        // Execute the command directly
                        await viewModel.GenerateAiShortCommand.ExecuteAsync(null);
                         Debug.WriteLine("[AiShortView.xaml.cs] AI Short Command execution awaited.");
                    }
                    catch (Exception ex)
                    {
                         Debug.WriteLine($"[AiShortView.xaml.cs] Exception during AI Short command execution: {ex}");
                         MessageBox.Show($"Error executing AI Short command: {ex.Message}", "Command Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                     Debug.WriteLine("[AiShortView.xaml.cs] AI Short Command CanExecute is false or command is null.");
                     MessageBox.Show("Cannot generate AI Short (Command CanExecute returned false or command is null). Check inputs/service status and logs.", "Command Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                     // Manually log the status check details if CanExecute is false
                     viewModel.LogCanGenerateAiShortStatus();
                }
            }
            else
            {
                 Debug.WriteLine("[AiShortView.xaml.cs] DataContext is NOT AiShortViewModel for AI Short button.");
                 MessageBox.Show("DataContext is not the expected AiShortViewModel.", "DataContext Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // --- MODIFIED Click Handler for Generate Script Button ---
        private async void GenerateScriptButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[AiShortView.xaml.cs] GenerateScriptButton_Click fired.");
            if (this.DataContext is AiShortViewModel viewModel)
            {
                 Debug.WriteLine("[AiShortView.xaml.cs] ViewModel found for Script button.");
                 
                 // Explicitly check if the command object is null first
                 if (viewModel.GenerateScriptCommand == null)
                 {
                     Debug.WriteLine("[AiShortView.xaml.cs] GenerateScriptCommand object is NULL.");
                     MessageBox.Show("Error: The GenerateScriptCommand object itself is null. Check ViewModel source generator.", "Command Null Error", MessageBoxButton.OK, MessageBoxImage.Error);
                     return; // Stop further execution
                 }

                 // REMOVED CanExecute check - Directly attempt execution
                 Debug.WriteLine("[AiShortView.xaml.cs] Attempting to execute Script Command via Click handler (bypassing CanExecute check)...");
                 try
                 {
                     await viewModel.GenerateScriptCommand.ExecuteAsync(null);
                     Debug.WriteLine("[AiShortView.xaml.cs] Script Command execution awaited (or threw).");
                 }
                 catch (Exception ex)
                 {
                      // Catch potential exceptions during execution itself
                      Debug.WriteLine($"[AiShortView.xaml.cs] Exception during Script command execution: {ex}");
                      MessageBox.Show($"Error executing script command: {ex.Message}", "Command Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 }
            }
             else
            {
                 Debug.WriteLine("[AiShortView.xaml.cs] DataContext is NOT AiShortViewModel for Script button.");
                 MessageBox.Show("DataContext is not the expected AiShortViewModel.", "DataContext Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // --- END MODIFIED ---
    }
}