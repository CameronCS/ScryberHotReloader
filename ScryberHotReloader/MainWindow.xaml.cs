using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ScryberHotReloader.Completions.CS;
using ScryberHotReloader.Completions.HTML;
using ScryberHotReloader.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shell;
using System.Xml;

namespace ScryberHotReloader {
    public partial class MainWindow : Window {
        private readonly MainViewModel _viewModel;
        private CompletionWindow? _htmlCompletionWindow;
        private CompletionWindow? _csCompletionWindow;

        public MainWindow(MainViewModel viewModel) {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // Subscribe to ViewModel events
            _viewModel.PdfGenerated += OnPdfGenerated;

            // Setup editor event handlers
            HtmlEditor.TextArea.TextEntered += TextArea_TextEntered;
            HtmlEditor.TextArea.TextEntering += TextArea_TextEntering;
            ModelEditor.TextArea.TextEntered += ModelEditor_TextEntered;
            ModelEditor.TextArea.TextEntering += ModelEditor_TextEntering;
            ModelEditor.TextArea.KeyDown += ModelEditor_KeyDown;

            // Load syntax highlighting
            LoadHtmlHighlighting();
            LoadCSHighlighting();

            // Setup window chrome
            this.StateChanged += (s, e) => UpdateMaximizeIcon();
            var chrome = new WindowChrome {
                CaptionHeight = 40,
                ResizeBorderThickness = new Thickness(10),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            // Set default content
            HtmlEditor.Text = Defaults.DefaultHtml;
            ModelEditor.Text = Defaults.DefaultCS;

            // Set configuration in ConfigTab
            ConfigTab.SetConfiguration(_viewModel.Configuration);
            ConfigTab.SetLoadedAssemblies(_viewModel.LoadedAssemblies);
        }

        #region Syntax Highlighting

        private void LoadCSHighlighting() {
            var uri = new Uri("pack://application:,,,/XSHDFiles/CSharpDark.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;

            if (stream != null) {
                using var reader = XmlReader.Create(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                ModelEditor.SyntaxHighlighting = highlighting;
            } else {
                MessageBox.Show("Failed to load C# syntax highlighting file.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadHtmlHighlighting() {
            var uri = new Uri("pack://application:,,,/XSHDFiles/HTMLDark.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;

            if (stream != null) {
                using var reader = new XmlTextReader(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HtmlEditor.SyntaxHighlighting = highlighting;
            } else {
                MessageBox.Show("Failed to load HTML syntax highlighting file.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Window Events

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            try {
                PdfViewer.Source = new Uri("about:blank");
            } catch { }

            _viewModel.Cleanup();
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                if (e.Key == Key.S) {
                    e.Handled = true;
                    await SaveHtml_Click_Async();
                } else if (e.Key == Key.O) {
                    e.Handled = true;
                    await OpenHtml_Click_Async();
                }
            }
        }

        #endregion

        #region File Operations

        private async void OpenHtml_Click(object sender, RoutedEventArgs e) {
            await OpenHtml_Click_Async();
        }

        private async Task OpenHtml_Click_Async() {
            await _viewModel.OpenHtmlFileAsync();
            HtmlEditor.Text = _viewModel.HtmlContent;
        }

        private async void SaveAsHtml_Click(object sender, RoutedEventArgs e) {
            await _viewModel.SaveHtmlFileAsAsync();
        }

        private async void SaveHtml_Click(object sender, RoutedEventArgs e) {
            await SaveHtml_Click_Async();
        }

        private async Task SaveHtml_Click_Async() {
            // Update ViewModel with current editor content
            _viewModel.HtmlContent = HtmlEditor.Text;
            _viewModel.ModelContent = ModelEditor.Text;

            // Update configuration from ConfigTab
            _viewModel.Configuration = ConfigTab.GetConfiguration();

            // Save and generate PDF
            await _viewModel.SaveAndGeneratePdfAsync();
        }

        #endregion

        #region PDF Generation

        private async void OnPdfGenerated(object? sender, string? pdfPath) {
            if (string.IsNullOrEmpty(pdfPath)) {
                return;
            }

            try {
                // Clear the WebView
                PdfViewer.Source = new Uri("about:blank");
                await Task.Delay(100);

                // Load the new PDF
                await PdfViewer.EnsureCoreWebView2Async();
                PdfViewer.CoreWebView2.Navigate($"file:///{pdfPath.Replace("\\", "/")}");
            } catch (Exception ex) {
                MessageBox.Show($"Failed to display PDF:\n{ex.Message}", "Display Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region HTML Auto-Completion

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e) {
            if (e.Text == "<") {
                _htmlCompletionWindow = new CompletionWindow(HtmlEditor.TextArea);
                IList<ICompletionData> data = _htmlCompletionWindow.CompletionList.CompletionData;

                foreach (string tag in HTMLAutoComplete.HtmlTags) {
                    data.Add(new HTMLCompletionData(tag));
                }

                _htmlCompletionWindow.Show();
                _htmlCompletionWindow.Closed += (o, args) => _htmlCompletionWindow = null;
            }
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e) {
            if (_htmlCompletionWindow == null)
                return;

            if (e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0])) {
                _htmlCompletionWindow.CompletionList.RequestInsertion(e);
            }
        }

        #endregion

        #region C# Auto-Completion

        private void ModelEditor_TextEntered(object sender, TextCompositionEventArgs e) {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '.') {
                return;
            }

            int caret = ModelEditor.CaretOffset;
            int start = TextUtilities.GetNextCaretPosition(ModelEditor.Document, caret,
                LogicalDirection.Backward, CaretPositioningMode.WordStart);
            if (start < 0) {
                start = 0;
            }

            int length = caret - start;
            string word = length > 0 ? ModelEditor.Document.GetText(start, length) : "";

            _csCompletionWindow = new(ModelEditor.TextArea);
            IList<ICompletionData> data = _csCompletionWindow.CompletionList.CompletionData;

            foreach (string keyword in CSAutoComplete.CSKeyWords) {
                if (keyword.StartsWith(word, StringComparison.InvariantCultureIgnoreCase)) {
                    data.Add(new CSCompletionData(keyword));
                }
            }

            if (data.Count > 0) {
                _csCompletionWindow.Show();
                _csCompletionWindow.Closed += (o, args) => _csCompletionWindow = null;
            } else {
                _csCompletionWindow = null;
            }
        }

        private void ModelEditor_TextEntering(object sender, TextCompositionEventArgs e) {
            if (_csCompletionWindow == null) {
                return;
            }

            if (e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0])) {
                _csCompletionWindow.CompletionList.RequestInsertion(e);
            }
        }

        private void ModelEditor_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                var caret = ModelEditor.TextArea.Caret;
                var line = ModelEditor.Document.GetLineByOffset(caret.Offset);
                var currentLineText = ModelEditor.Document.GetText(line.Offset, line.Length);

                // Get leading whitespace
                string indentation = new([.. currentLineText.TakeWhile(char.IsWhiteSpace)]);

                // Insert newline and indentation
                ModelEditor.Document.Insert(caret.Offset, Environment.NewLine + indentation);
                caret.Offset += Environment.NewLine.Length + indentation.Length;

                e.Handled = true;
            }
        }

        #endregion

        #region Window Controls

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e) {
            ToggleWindowState();
        }

        private void ToggleWindowState() {
            if (WindowState == WindowState.Normal) {
                WindowState = WindowState.Maximized;
            } else {
                WindowState = WindowState.Normal;
            }
        }

        private void UpdateMaximizeIcon() {
            if (WindowState == WindowState.Maximized) {
                MaximizeButton.Content = "\uE923"; // Restore icon
            } else {
                MaximizeButton.Content = "\uE922"; // Maximize icon
            }
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                ToggleWindowState();
            } else {
                DragMove();
            }
        }

        #endregion
    }
}
