using System.Windows;
using System.Windows.Input;

namespace WhiteLabelLauncher
{
    public partial class AddTabWindow : Window
    {
        public string ResultName { get; private set; } = "";
        public string ResultIconCode { get; private set; } = "\uE71D";

        public AddTabWindow()
        {
            InitializeComponent();
            TxtCategoryName.Focus();
        }

        public void InitForEdit(CategoryModel cat)
        {
            TxtCategoryName.Text = cat.CategoryName;
            
            // Check the corresponding radio button
            foreach (var child in IconWrapPanel.Children)
            {
                if (child is System.Windows.Controls.RadioButton rb && rb.Tag?.ToString() == cat.IconCode)
                {
                    rb.IsChecked = true;
                    break;
                }
            }
        }

        private void WindowDrag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var text = TxtCategoryName.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                ErrorMsg.Text = "Category name cannot be empty.";
                ErrorMsg.Visibility = Visibility.Visible;
                return;
            }

            ResultName = text;
            
            // Extract selected icon from wrap panel
            foreach (var child in IconWrapPanel.Children)
            {
                if (child is System.Windows.Controls.RadioButton rb && rb.IsChecked == true)
                {
                    ResultIconCode = rb.Tag?.ToString() ?? "\uE71D";
                    break;
                }
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
