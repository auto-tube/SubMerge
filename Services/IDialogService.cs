using System.Threading.Tasks; // Although MessageBox is sync, interface could allow async later

namespace AutoTubeWpf.Services
{
    /// <summary>
    /// Defines different types of dialog results (e.g., Yes/No).
    /// </summary>
    public enum DialogResult
    {
        Undefined,
        Ok,
        Cancel,
        Yes,
        No
    }

    /// <summary>
    /// Defines the contract for showing dialogs to the user.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an informational message dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog window.</param>
        void ShowInfoDialog(string message, string title = "Information");

        /// <summary>
        /// Shows a warning message dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog window.</param>
        void ShowWarningDialog(string message, string title = "Warning");

        /// <summary>
        /// Shows an error message dialog.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the dialog window.</param>
        void ShowErrorDialog(string message, string title = "Error");

        /// <summary>
        /// Shows a confirmation dialog with Yes/No options.
        /// </summary>
        /// <param name="message">The confirmation question to display.</param>
        /// <param name="title">The title of the dialog window.</param>
        /// <returns>DialogResult.Yes or DialogResult.No.</returns>
        DialogResult ShowConfirmationDialog(string message, string title = "Confirm");
    }
}