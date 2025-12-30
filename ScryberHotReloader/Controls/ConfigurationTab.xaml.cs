using ScryberHotReloader.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ScryberHotReloader.Controls {
    public partial class ConfigurationTab : UserControl {
        private ScryberConfiguration _configuration;

        public ConfigurationTab() {
            InitializeComponent();
            _configuration = new ScryberConfiguration();

            PageSizeComboBox.SelectionChanged += PageSizeComboBox_SelectionChanged;
        }

        public ScryberConfiguration GetConfiguration() {
            _configuration.PageSize = ((ComboBoxItem)PageSizeComboBox.SelectedItem).Content.ToString() ?? "A4";
            _configuration.PageOrientation = ((ComboBoxItem)OrientationComboBox.SelectedItem).Content.ToString() ?? "Portrait";

            // Parse custom dimensions if "Custom" is selected
            if (_configuration.PageSize == "Custom") {
                if (double.TryParse(PageWidthTextBox.Text, out double width)) {
                    _configuration.PageWidth = width;
                }
                if (double.TryParse(PageHeightTextBox.Text, out double height)) {
                    _configuration.PageHeight = height;
                }
            }

            // Parse margins
            if (double.TryParse(MarginTopTextBox.Text, out double top)) {
                _configuration.MarginTop = top;
            }
            if (double.TryParse(MarginBottomTextBox.Text, out double bottom)) {
                _configuration.MarginBottom = bottom;
            }
            if (double.TryParse(MarginLeftTextBox.Text, out double left)) {
                _configuration.MarginLeft = left;
            }
            if (double.TryParse(MarginRightTextBox.Text, out double right)) {
                _configuration.MarginRight = right;
            }

            // Parse font paths
            var fontPaths = FontPathsTextBox.Text
                .Split('\n')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            _configuration.FontPaths = fontPaths;

            return _configuration;
        }

        public void SetConfiguration(ScryberConfiguration config) {
            _configuration = config;

            // Set page size
            foreach (ComboBoxItem item in PageSizeComboBox.Items) {
                if (item.Content.ToString() == config.PageSize) {
                    PageSizeComboBox.SelectedItem = item;
                    break;
                }
            }

            // Set orientation
            foreach (ComboBoxItem item in OrientationComboBox.Items) {
                if (item.Content.ToString() == config.PageOrientation) {
                    OrientationComboBox.SelectedItem = item;
                    break;
                }
            }

            // Set custom dimensions
            if (config.PageWidth.HasValue) {
                PageWidthTextBox.Text = config.PageWidth.Value.ToString();
            }
            if (config.PageHeight.HasValue) {
                PageHeightTextBox.Text = config.PageHeight.Value.ToString();
            }

            // Set margins
            MarginTopTextBox.Text = config.MarginTop.ToString();
            MarginBottomTextBox.Text = config.MarginBottom.ToString();
            MarginLeftTextBox.Text = config.MarginLeft.ToString();
            MarginRightTextBox.Text = config.MarginRight.ToString();

            // Set font paths
            if (config.FontPaths != null && config.FontPaths.Count > 0) {
                FontPathsTextBox.Text = string.Join("\n", config.FontPaths);
            }
        }

        public void SetLoadedAssemblies(System.Collections.Generic.IEnumerable<string> assemblies) {
            LoadedAssembliesList.ItemsSource = assemblies;
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (PageSizeComboBox.SelectedItem is ComboBoxItem item) {
                CustomDimensionsPanel.Visibility = item.Content.ToString() == "Custom"
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }
}
