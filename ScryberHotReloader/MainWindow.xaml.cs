using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Win32;
using Scryber.Components;
using ScryberHotReloader.Completions.CS;
using ScryberHotReloader.Completions.HTML;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml;
using System.Windows.Shell;

namespace ScryberHotReloader {
    public partial class MainWindow : Window {
        private readonly string CWD = Directory.GetCurrentDirectory();
        private string _currentHtml = "";
        private string _previousFile = "";
        private string _currentFilePath = "";
        private CompletionWindow? _htmlCompletionWindow;
        private CompletionWindow? _csCompletionWindow;

        public MainWindow() {
            InitializeComponent();
            HtmlEditor.TextArea.TextEntered += TextArea_TextEntered;
            HtmlEditor.TextArea.TextEntering += TextArea_TextEntering;
            ModelEditor.TextArea.TextEntered += ModelEditor_TextEntered;
            ModelEditor.TextArea.TextEntering += ModelEditor_TextEntering;
            ModelEditor.TextArea.KeyDown += ModelEditor_KeyDown;
            LoadHtmlHighlighting();
            LoadCSHighlighting();
            this.StateChanged += (s, e) => UpdateMaximizeIcon();

            var chrome = new WindowChrome {
                CaptionHeight = 40,
                ResizeBorderThickness = new Thickness(10),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            };

            WindowChrome.SetWindowChrome(this, chrome);
            HtmlEditor.Text = Defaults.DefaultHtml;
            ModelEditor.Text = Defaults.DefaultCS;
        }

        private void LoadCSHighlighting() {
            var uri = new Uri("pack://application:,,,/XSHDFiles/CSharpDark.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;

            if (stream != null) {
                using var reader = XmlReader.Create(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                ModelEditor.SyntaxHighlighting = highlighting;
            } else {
                MessageBox.Show("Failed to load syntax highlighting file.");
            }
        }

        private void LoadHtmlHighlighting() {
            using var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/XSHDFiles/HTMLDark.xshd")).Stream;

            using var reader = new XmlTextReader(stream);
            var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);

            HtmlEditor.SyntaxHighlighting = highlighting;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            try {
                PdfViewer.Source = new Uri("about:blank");
            } catch { }

            try {
                if (!string.IsNullOrEmpty(_previousFile) && File.Exists(_previousFile)) {
                    File.Delete(_previousFile);
                }
            } catch { /* Ignore errors silently */ }
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                if (e.Key == Key.S) {
                    e.Handled = true;
                    await SaveHtml_Click_Async();
                } else if (e.Key == Key.O) {
                    e.Handled = true;
                    OpenHtml_Click(sender, new RoutedEventArgs());
                }
            }
        }

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

        private void OpenHtml_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dlg = new() {
                Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*",
                Title = "Open HTML File"
            };

            if (dlg.ShowDialog() == true) {
                try {
                    string content = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                    HtmlEditor.Text = content;
                    _currentHtml = content;
                    _currentFilePath = dlg.FileName;
                    SaveHtml_Click(sender, e);
                } catch (Exception ex) {
                    MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsHtml_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog dlg = new() {
                Filter = "HTML Files (*.html)|*.html|All files (*.*)|*.*",
                Title = "Save HTML File As"
            };

            if (dlg.ShowDialog() == true) {
                try {
                    File.WriteAllText(dlg.FileName, HtmlEditor.Text, Encoding.UTF8);
                    _currentHtml = HtmlEditor.Text;
                    _currentFilePath = dlg.FileName;
                } catch (Exception ex) {
                    MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SaveHtml_Click(object sender, RoutedEventArgs e) {
            await SaveHtml_Click_Async();
        }

        private async Task SaveHtml_Click_Async() {
            if (string.IsNullOrEmpty(_currentFilePath)) {
                SaveAsHtml_Click(this, new RoutedEventArgs());
            }

            _currentHtml = HtmlEditor.Text;
            File.WriteAllText(_currentFilePath, _currentHtml, Encoding.UTF8);

            string modelCode = ModelEditor.Text;
            object? modelInstance = CompileAndInstantiateAnyModel(modelCode);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string output = System.IO.Path.Combine(CWD, $"preview_{timestamp}.pdf");

            try {
                Document document = Document.ParseDocument(_currentFilePath);

                if (modelInstance != null) {
                    document.Params["Model"] = modelInstance;
                }

                await document.SaveAsPDFAsync(output);

                PdfViewer.Source = new Uri("about:blank");
                await Task.Delay(100);

                if (!string.IsNullOrEmpty(_previousFile) && File.Exists(_previousFile)) {
                    try {
                        File.Delete(_previousFile);
                    } catch { /* Ignore Errors Silently */ }
                }

                _previousFile = output;
                await PdfViewer.EnsureCoreWebView2Async();
                PdfViewer.CoreWebView2.Navigate($"file:///{output.Replace("\\", "/")}");
            } catch (Exception ex) {
                MessageBox.Show($"Failed to generate PDF:\n{ex.Message}", "PDF Error",MessageBoxButton.OK, MessageBoxImage.Warning);
                
            }
        }

        private void ModelEditor_TextEntered(object sender, TextCompositionEventArgs e) {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '.') {
                return;
            }

            int caret = ModelEditor.CaretOffset;
            int start = TextUtilities.GetNextCaretPosition(ModelEditor.Document, caret, LogicalDirection.Backward, CaretPositioningMode.WordStart);
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


        public static object? CompileAndInstantiateAnyModel(string sourceCode) {
            if (string.IsNullOrEmpty(sourceCode)) {
                return null;
            }

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            List<MetadataReference> refs = [.. AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => MetadataReference.CreateFromFile(a.Location)).Cast<MetadataReference>()];

            CSharpCompilation compilation = CSharpCompilation.Create("DynamicModelAssembly", [syntaxTree], refs, new(OutputKind.DynamicallyLinkedLibrary));

            using MemoryStream ms = new();
            Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(ms);

            if (!result.Success) {
                string errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

                MessageBox.Show($"Model compilation failed:\n\n{errors}", "Compile Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            ms.Seek(0, SeekOrigin.Begin);
            Assembly assembly = Assembly.Load(ms.ToArray());

            Type? modelType = assembly.GetTypes().FirstOrDefault(t => t.GetConstructor([]) != null);

            if (modelType == null) {
                MessageBox.Show("No suitable public class with a parameterless constructor was found in the model.", "Compile Error");
                return null;
            }

            return Activator.CreateInstance(modelType);
        }

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
    }
}