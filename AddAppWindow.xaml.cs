using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WhiteLabelLauncher
{
    public partial class AddAppWindow : Window
    {
        // ─────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────
        /// <summary>Result read by MainWindow after ShowDialog() == true.</summary>
        public AppEntry? Result { get; private set; }

        /// <summary>Path of the selected custom icon image (empty = none chosen).</summary>
        private string _iconPath = "";

        public AddAppWindow() => InitializeComponent();

        // ─────────────────────────────────────────────────────────────────
        //  Window drag
        // ─────────────────────────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Placeholder text helpers
        // ─────────────────────────────────────────────────────────────────
        private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => NameHint.Visibility = string.IsNullOrEmpty(NameBox.Text)
               ? Visibility.Visible : Visibility.Collapsed;

        private void PathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => PathHint.Visibility = string.IsNullOrEmpty(PathBox.Text)
               ? Visibility.Visible : Visibility.Collapsed;

        // ─────────────────────────────────────────────────────────────────
        //  Browse for .exe
        // ─────────────────────────────────────────────────────────────────
        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select the application executable",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                PathBox.Text = dlg.FileName;

                // Auto-populate name from exe filename if field is still empty
                if (string.IsNullOrWhiteSpace(NameBox.Text))
                    NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Browse for custom icon image
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Clicking the preview square also opens the browse dialog.</summary>
        private void IconPreview_Click(object sender, MouseButtonEventArgs e)
            => BrowseIcon_Click(sender, e);

        private void BrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select an icon image for this app",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.webp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                ApplyIconPath(dlg.FileName);
        }

        private void ClearIcon_Click(object sender, RoutedEventArgs e)
            => ApplyIconPath("");

        /// <summary>
        /// Updates the preview square, filename label, and Clear-button visibility
        /// to reflect the chosen image path. Pass "" to reset to the placeholder.
        /// </summary>
        private void ApplyIconPath(string path)
        {
            _iconPath = path;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                // Load into the preview image (BitmapImage with caching avoids file locks)
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource        = new Uri(path);
                bmp.CacheOption      = BitmapCacheOption.OnLoad;
                bmp.CreateOptions    = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();

                IconPreviewImage.Source      = bmp;
                IconPreviewImage.Visibility  = Visibility.Visible;
                IconPreviewPlaceholder.Visibility = Visibility.Collapsed;

                IconFileNameLabel.Text      = Path.GetFileName(path);
                ClearIconBtn.Visibility     = Visibility.Visible;
            }
            else
            {
                // Reset to placeholder
                IconPreviewImage.Source      = null;
                IconPreviewImage.Visibility  = Visibility.Collapsed;
                IconPreviewPlaceholder.Visibility = Visibility.Visible;

                IconFileNameLabel.Text      = "No image selected";
                ClearIconBtn.Visibility     = Visibility.Collapsed;
                _iconPath = "";
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Save
        // ─────────────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) ||
                string.IsNullOrWhiteSpace(PathBox.Text))
            {
                MessageBox.Show(
                    "Please enter both an App Name and an Executable Path.",
                    "Launcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Result = new AppEntry
            {
                Id       = Guid.NewGuid().ToString(),
                Name     = NameBox.Text.Trim(),
                ExePath  = PathBox.Text.Trim(),
                IconPath = _iconPath           // "" if none chosen → gradient tile
            };

            DialogResult = true;
            Close();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Cancel
        // ─────────────────────────────────────────────────────────────────
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
