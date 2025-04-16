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
            // Try showing directly, assuming it's called after UI thread is available or during startup error handling
            try { MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DialogService] Direct MessageBox failed: {ex.Message}"); }
        }

        public void ShowWarningDialog(string message, string title = "Warning")
        {
             try { MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DialogService] Direct MessageBox failed: {ex.Message}"); }
        }

        public void ShowErrorDialog(string message, string title = "Error")
        {
             try { MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DialogService] Direct MessageBox failed: {ex.Message}"); }
        }

        public DialogResult ShowConfirmationDialog(string message, string title = "Confirm")
        {
            MessageBoxResult result = MessageBoxResult.No; // Default to No
             try { result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DialogService] Direct MessageBox failed: {ex.Message}"); }

            return result switch
            {
                MessageBoxResult.Yes => DialogResult.Yes,
                MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.No // Default to No for other cases (Cancel, None, OK)
            };
        }
    }
}