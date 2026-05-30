using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace WhiteLabelLauncher
{
    public partial class AddProjectWindow : Window
    {
        public ProjectModel? Result { get; private set; }

        public AddProjectWindow(IEnumerable<AppEntry> availableApps)
        {
            InitializeComponent();
            AppSelector.ItemsSource = availableApps;
        }

        private void WindowDrag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select a project file",
                Filter = "All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                PathBox.Text = dlg.FileName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathBox.Text))
            {
                MessageBox.Show("Please select a project file.", "Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedApp = AppSelector.SelectedItem as AppEntry;

            Result = new ProjectModel
            {
                FileName = Path.GetFileNameWithoutExtension(PathBox.Text),
                FilePath = PathBox.Text,
                Extension = Path.GetExtension(PathBox.Text),
                LastModified = File.Exists(PathBox.Text) ? File.GetLastWriteTime(PathBox.Text) : DateTime.Now,
                AppId = selectedApp?.Id ?? ""
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
