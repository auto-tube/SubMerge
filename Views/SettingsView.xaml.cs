using AutoTubeWpf.ViewModels; // Added for ViewModel access
using System.Windows; // Added for RoutedEventArgs
using System.Windows.Controls;

namespace AutoTubeWpf.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        // Event handler for PasswordBox to update ViewModel
        private void AwsSecretKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel && sender is PasswordBox passwordBox)
            {
                viewModel.AwsSecretAccessKey = passwordBox.Password;
            }
        }
    }
}