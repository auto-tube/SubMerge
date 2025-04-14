using System.Windows; // Required for MessageBox

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Basic implementation of IDialogService using System.Windows.MessageBox.
    /// </summary>
    public class DialogService : IDialogService
    {
        // Inject ILoggerService if logging dialog actions is desired
        // private readonly ILoggerService _logger;
        // public DialogService(ILoggerService logger) { _logger = logger; }
        public DialogService() { } // Simple constructor for now

        public void ShowInfoDialog(string message, string title = "Information")
        {
            // Ensure dialogs are shown on the UI thread if called from background threads
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public void ShowWarningDialog(string message, string title = "Warning")
        {
             Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        public void ShowErrorDialog(string message, string title = "Error")
        {
             Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public DialogResult ShowConfirmationDialog(string message, string title = "Confirm")
        {
            MessageBoxResult result = MessageBoxResult.No; // Default to No
             Application.Current.Dispatcher.Invoke(() =>
            {
                 result = MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            });

            return result switch
            {
                MessageBoxResult.Yes => DialogResult.Yes,
                MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.No // Default to No for other cases (Cancel, None, OK)
            };
        }
    }
}