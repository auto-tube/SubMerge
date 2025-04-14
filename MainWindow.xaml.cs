using MahApps.Metro.Controls; // Required for MetroWindow

namespace AutoTubeWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow // Inherit from MetroWindow
    {
        // Constructor can optionally accept the ViewModel via DI
        // public MainWindow(ViewModels.MainWindowViewModel viewModel)
        public MainWindow()
        {
            InitializeComponent();
            // DataContext is set via DI in App.xaml.cs or could be set here if needed:
            // DataContext = viewModel;
        }
    }
}