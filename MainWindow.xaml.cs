using System.Windows; // Standard Window class

namespace AutoTubeWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window // Inherit from standard Window
    {
        // Constructor accepts the ViewModel via DI
        public MainWindow(ViewModels.MainWindowViewModel viewModel)
        // public MainWindow() // Comment out parameterless constructor
        {
            InitializeComponent();
            // Set DataContext explicitly
            DataContext = viewModel;
        }
    }
}