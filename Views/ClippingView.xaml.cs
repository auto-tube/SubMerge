using AutoTubeWpf.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AutoTubeWpf.Views
{
    /// <summary>
    /// Interaction logic for ClippingView.xaml
    /// </summary>
    public partial class ClippingView : UserControl
    {
        public ClippingView()
        {
            InitializeComponent();
        }

        private void ClippingView_Drop(object sender, DragEventArgs e)
        {
            // Ensure the DataContext is the correct ViewModel
            if (DataContext is ClippingViewModel viewModel)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Note: This returns an array of paths (files and/or folders)
                    string[]? droppedItems = e.Data.GetData(DataFormats.FileDrop) as string[];

                    if (droppedItems != null &amp;&amp; droppedItems.Length > 0)
                    {
                        viewModel.Logger?.LogDebug($"Drop event detected with {droppedItems.Length} item(s).");

                        var filesToAdd = droppedItems.Where(File.Exists).ToArray();
                        var directoriesToAdd = droppedItems.Where(Directory.Exists).ToArray();

                        if (filesToAdd.Length > 0)
                        {
                            viewModel.AddFilesToQueue(filesToAdd);
                        }

                        foreach (var dir in directoriesToAdd)
                        {
                            viewModel.AddDirectoryToQueue(dir);
                        }
                    }
                     else
                    {
                         viewModel.Logger?.LogDebug("Drop event occurred but no valid file/folder data found.");
                    }
                }
                 else
                {
                     viewModel.Logger?.LogDebug("Drop event occurred but data format was not FileDrop.");
                }
            }
             else
            {
                 // Log error if DataContext is wrong type or null
                 System.Diagnostics.Debug.WriteLine("[ClippingView_Drop] Error: DataContext is not a ClippingViewModel.");
            }

            // Mark the event as handled
            e.Handled = true;
        }
    }
}