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
using static ScryberHotReloader.Completions.CS.ModelIntelliSense;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace ScryberHotReloader {
    public partial class MainWindow : Window {
        private readonly string CWD = Directory.GetCurrentDirectory();
        private string _currentHtml = "";
        private string _previousFile = "";
        private string _currentFilePath = "";
        private CompletionWindow? _htmlCompletionWindow;
        private CompletionWindow? _csCompletionWindow;
        private List<int> _findMatches = [];
        private int _currentMatchIndex = -1;
        private IServiceProvider? _serviceProvider;

        public MainWindow() {
            InitializeComponent();
            HtmlEditor.TextArea.TextEntered += TextArea_TextEntered;
            HtmlEditor.TextArea.TextEntering += TextArea_TextEntering;
            ModelEditor.TextArea.TextEntered += ModelEditor_TextEntered;
            ModelEditor.TextArea.TextEntering += ModelEditor_TextEntering;
            ModelEditor.TextArea.KeyDown += ModelEditor_KeyDown;
            StartupEditor.TextArea.TextEntered += ModelEditor_TextEntered;
            StartupEditor.TextArea.TextEntering += ModelEditor_TextEntering;
            StartupEditor.TextArea.KeyDown += ModelEditor_KeyDown;
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
            StartupEditor.Text = Defaults.DefaultStartup;
        }

        private void LoadCSHighlighting() {
            var uri = new Uri("pack://application:,,,/XSHDFiles/CSharpDark.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;

            if (stream != null) {
                using var reader = XmlReader.Create(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                ModelEditor.SyntaxHighlighting = highlighting;
                StartupEditor.SyntaxHighlighting = highlighting;
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
                } else if (e.Key == Key.F) {
                    e.Handled = true;
                    OpenFindPanel(showReplace: false);
                } else if (e.Key == Key.H) {
                    e.Handled = true;
                    OpenFindPanel(showReplace: true);
                }
            } else if (e.Key == Key.Escape && FindReplacePanel.Visibility == Visibility.Visible) {
                e.Handled = true;
                CloseFind();
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

                    // Load companion startup file if one exists alongside the HTML
                    string startupPath = System.IO.Path.ChangeExtension(dlg.FileName, ".startup.cs");
                    if (File.Exists(startupPath))
                        StartupEditor.Text = File.ReadAllText(startupPath, Encoding.UTF8);

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

            // Save startup tab alongside the HTML so it persists between sessions
            string startupFilePath = System.IO.Path.ChangeExtension(_currentFilePath, ".startup.cs");
            File.WriteAllText(startupFilePath, StartupEditor.Text, Encoding.UTF8);

            // 1. Load plugin assemblies so types are available to Roslyn and the Startup tab
            var (assemblies, pluginPaths, appSettingsPath, asmWarnings) = PluginLoader.LoadAssembliesOnly(_currentFilePath);

            // 2. Compile the Startup tab — takes priority over convention registrar
            _serviceProvider = CompileStartupServices(StartupEditor.Text, pluginPaths, appSettingsPath);

            // 3. Fall back to convention registrar (ScryberPluginRegistrar / Program / Startup)
            if (_serviceProvider == null && assemblies.Count > 0) {
                var (fallback, regWarnings) = PluginLoader.BuildFromRegistrar(assemblies);
                _serviceProvider = fallback;
                var allWarnings = asmWarnings.Concat(regWarnings).ToArray();
                if (allWarnings.Length > 0)
                    MessageBox.Show(string.Join("\n\n", allWarnings), "Plugin Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
            } else if (asmWarnings.Length > 0) {
                MessageBox.Show(string.Join("\n\n", asmWarnings), "Plugin Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            string modelCode = ModelEditor.Text;
            var models = CompileAndRunModel(modelCode, _serviceProvider);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string output = System.IO.Path.Combine(CWD, $"preview_{timestamp}.pdf");

            try {
                Document document = Document.ParseDocument(_currentFilePath);

                if (models != null) {
                    foreach (var kvp in models)
                        document.Params[kvp.Key] = kvp.Value;
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
            int caret = ModelEditor.CaretOffset;

            // --- Dot: member access completion ---
            if (e.Text == ".") {
                string src = ModelEditor.Text;

                // Walk back from the dot to find the identifier before it
                int nameEnd = caret - 1;
                int nameStart = nameEnd;
                while (nameStart > 0 && (char.IsLetterOrDigit(src[nameStart - 1]) || src[nameStart - 1] == '_'))
                    nameStart--;

                string exprName = src[nameStart..nameEnd];
                if (string.IsNullOrEmpty(exprName)) return;

                var memberItems = ModelIntelliSense.GetMemberCompletions(src, exprName).ToList();
                if (memberItems.Count == 0) return;

                _csCompletionWindow = new CompletionWindow(ModelEditor.TextArea);
                _csCompletionWindow.StartOffset = caret; // replace only what's typed after the dot
                foreach (var item in memberItems)
                    _csCompletionWindow.CompletionList.CompletionData.Add(item);

                _csCompletionWindow.Show();
                _csCompletionWindow.Closed += (_, _) => _csCompletionWindow = null;
                return;
            }

            // --- Alphanumeric: keyword + type name completion ---
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_') return;

            int wordStart = TextUtilities.GetNextCaretPosition(
                ModelEditor.Document, caret, LogicalDirection.Backward, CaretPositioningMode.WordStart);
            if (wordStart < 0) wordStart = 0;

            string word = caret > wordStart ? ModelEditor.Document.GetText(wordStart, caret - wordStart) : "";
            if (string.IsNullOrEmpty(word)) return;

            var items = new List<ICompletionData>();

            foreach (string keyword in CSAutoComplete.CSKeyWords)
                if (keyword.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                    items.Add(new CSCompletionData(keyword));

            items.AddRange(ModelIntelliSense.GetTypeCompletions(word));

            if (items.Count == 0) return;

            _csCompletionWindow = new CompletionWindow(ModelEditor.TextArea);
            // Set StartOffset to word start so the full typed prefix is replaced on completion
            _csCompletionWindow.StartOffset = wordStart;
            foreach (var item in items)
                _csCompletionWindow.CompletionList.CompletionData.Add(item);

            _csCompletionWindow.Show();
            _csCompletionWindow.Closed += (_, _) => _csCompletionWindow = null;
        }

        private void ModelEditor_TextEntering(object sender, TextCompositionEventArgs e) {
            if (_csCompletionWindow == null) return;
            if (e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
                _csCompletionWindow.CompletionList.RequestInsertion(e);
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


        private static IServiceProvider? CompileStartupServices(string sourceCode, string[]? pluginPaths = null, string? appSettingsPath = null) {
            if (string.IsNullOrWhiteSpace(sourceCode)) return null;

            var refPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var refNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var refs     = new List<MetadataReference>();

            // Step 1 — AppDomain assemblies first. They establish the canonical version of every
            // assembly the hot reloader is compiled against (System.Runtime, DI.Abstractions, etc.).
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                if (a.IsDynamic || string.IsNullOrEmpty(a.Location) || !refPaths.Add(a.Location)) continue;
                refNames.Add(a.GetName().Name ?? "");
                try { refs.Add(MetadataReference.CreateFromFile(a.Location)); } catch { }
            }

            // Step 2 — add a DLL by path, skipping if the same path OR same assembly name is already
            // covered. This prevents CS0433/CS0518 from duplicate/conflicting versions of the same
            // assembly (e.g. System.Runtime v9 from AppDomain vs v10 from a .NET 10 framework dir).
            void TryAddRef(string path) {
                if (!refPaths.Add(path) || !File.Exists(path)) return;
                try {
                    var name = AssemblyName.GetAssemblyName(path); // throws for native DLLs
                    if (name.Name != null && !refNames.Add(name.Name)) return;
                    refs.Add(MetadataReference.CreateFromFile(path));
                } catch { }
            }

            // Plugin paths from config — types visible to Roslyn even if runtime load failed
            if (pluginPaths != null)
                foreach (var p in pluginPaths) TryAddRef(p);

            // Probe directories (bin folder + shared frameworks) — only adds names not yet covered,
            // so Microsoft.AspNetCore.* comes in but System.Runtime / DI.Abstractions are skipped.
            foreach (var dir in PluginLoader.GetProbeDirectories()) {
                if (!Directory.Exists(dir)) continue;
                foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                    TryAddRef(dll);
            }

            var compilation = CSharpCompilation.Create(
                "StartupAssembly",
                [CSharpSyntaxTree.ParseText(sourceCode)],
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success) {
                string errors = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                MessageBox.Show($"Startup compilation failed:\n\n{errors}", "Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            // --- Static ConfigureServices(IServiceCollection) — original convention ---
            MethodInfo? staticMethod = assembly.GetTypes()
                .Select(t => t.GetMethod("ConfigureServices",
                    BindingFlags.Public | BindingFlags.Static,
                    [typeof(IServiceCollection)]))
                .FirstOrDefault(m => m != null);

            // --- Instance ConfigureServices(IServiceCollection) — ASP.NET Core Startup pattern ---
            Type? instanceType = null;
            MethodInfo? instanceMethod = null;
            if (staticMethod == null) {
                foreach (var t in assembly.GetTypes()) {
                    var im = t.GetMethod("ConfigureServices",
                        BindingFlags.Public | BindingFlags.Instance,
                        [typeof(IServiceCollection)]);
                    if (im != null) { instanceType = t; instanceMethod = im; break; }
                }
            }

            if (staticMethod == null && instanceMethod == null) return null; // nothing to call — not an error

            var services = new ServiceCollection();

            if (staticMethod != null) {
                try {
                    staticMethod.Invoke(null, [services]);
                } catch (Exception ex) {
                    MessageBox.Show($"ConfigureServices threw an exception:\n\n{ex.InnerException?.Message ?? ex.Message}",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            } else {
                // Build IConfiguration from the appsettings path if provided
                IConfiguration? configuration = null;
                if (!string.IsNullOrEmpty(appSettingsPath) && File.Exists(appSettingsPath)) {
                    try {
                        configuration = new ConfigurationBuilder()
                            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false)
                            .Build();
                    } catch (Exception ex) {
                        MessageBox.Show($"Failed to load appsettings.json:\n\n{ex.Message}",
                            "Startup Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // Instantiate the Startup class: IConfiguration ctor, then parameterless
                object? startupInstance;
                var configCtor = instanceType!.GetConstructor([typeof(IConfiguration)]);
                if (configCtor != null) {
                    if (configuration == null) {
                        MessageBox.Show(
                            "The Startup class requires IConfiguration but no appsettings.json path is set.\n\n" +
                            "Set it via Plugins → Manage Plugins...",
                            "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                    try {
                        startupInstance = configCtor.Invoke([configuration]);
                    } catch (Exception ex) {
                        MessageBox.Show($"Failed to instantiate Startup:\n\n{ex.InnerException?.Message ?? ex.Message}",
                            "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                } else {
                    try {
                        startupInstance = Activator.CreateInstance(instanceType!);
                    } catch (Exception ex) {
                        MessageBox.Show($"Failed to instantiate Startup:\n\n{ex.InnerException?.Message ?? ex.Message}",
                            "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                }

                try {
                    instanceMethod!.Invoke(startupInstance, [services]);
                } catch (Exception ex) {
                    MessageBox.Show($"ConfigureServices threw an exception:\n\n{ex.InnerException?.Message ?? ex.Message}",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }

            return services.BuildServiceProvider();
        }

        public static Dictionary<string, IScryberModel>? CompileAndRunModel(string sourceCode, IServiceProvider? services = null) {
            if (string.IsNullOrEmpty(sourceCode)) return null;

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            var compilation = CSharpCompilation.Create("DynamicModelAssembly", [syntaxTree], refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success) {
                string errors = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                MessageBox.Show($"Model compilation failed:\n\n{errors}", "Compile Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var runnerTypes = assembly.GetTypes()
                .Where(t => typeof(IScryberRunner).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (runnerTypes.Count == 0) {
                MessageBox.Show("No class implementing IScryberRunner was found in the Model tab.", "Model Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            if (runnerTypes.Count > 1) {
                string names = string.Join(", ", runnerTypes.Select(t => t.Name));
                MessageBox.Show($"Only one IScryberRunner is allowed per Model tab, but found {runnerTypes.Count}: {names}", "Model Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            try {
                if (services != null) {
                    // Create a scope so DbContext and other scoped services resolve correctly.
                    // The scope lives only for the duration of GetModels() then disposes cleanly.
                    using var scope = services.CreateScope();
                    var runner = (IScryberRunner)ActivatorUtilities.CreateInstance(scope.ServiceProvider, runnerTypes[0]);
                    return runner.GetModels();
                } else {
                    var runner = (IScryberRunner)Activator.CreateInstance(runnerTypes[0])!;
                    return runner.GetModels();
                }
            } catch (Exception ex) {
                var chain = new System.Text.StringBuilder();
                var e = (Exception?)ex;
                while (e != null) { chain.AppendLine(e.Message); e = e.InnerException; }
                MessageBox.Show($"Failed to run model:\n\n{chain}", "Model Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
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

        private void ManagePlugins_Click(object sender, RoutedEventArgs e) {
            string dir = !string.IsNullOrEmpty(_currentFilePath)
                ? System.IO.Path.GetDirectoryName(_currentFilePath)!
                : Directory.GetCurrentDirectory();

            string configPath = System.IO.Path.Combine(dir, "scryber-plugins.json");

            var dialog = new PluginManagerWindow(configPath) { Owner = this };
            dialog.ShowDialog();

            if (dialog.Saved) {
                var (provider, warnings) = PluginLoader.Load(_currentFilePath);
                _serviceProvider = provider;

                if (warnings.Length > 0)
                    MessageBox.Show(string.Join("\n\n", warnings), "Plugin Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PresetManager_Click(object sender, RoutedEventArgs e) {
            PluginConfig? current = null;
            if (!string.IsNullOrEmpty(_currentFilePath)) {
                string cfgPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(_currentFilePath)!, "scryber-plugins.json");
                if (File.Exists(cfgPath)) {
                    try { current = System.Text.Json.JsonSerializer.Deserialize<PluginConfig>(
                              File.ReadAllText(cfgPath),
                              new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    } catch { }
                }
            }

            var dialog = new PresetManagerWindow(StartupEditor.Text, current) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.LoadedPreset != null)
                ApplyPreset(dialog.LoadedPreset);
        }

        private void SaveAsPreset_Click(object sender, RoutedEventArgs e) {
            PluginConfig? current = null;
            if (!string.IsNullOrEmpty(_currentFilePath)) {
                string cfgPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(_currentFilePath)!, "scryber-plugins.json");
                if (File.Exists(cfgPath)) {
                    try { current = System.Text.Json.JsonSerializer.Deserialize<PluginConfig>(
                              File.ReadAllText(cfgPath),
                              new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    } catch { }
                }
            }

            var dialog = new PresetManagerWindow(StartupEditor.Text, current) { Owner = this };
            dialog.ShowDialog();
        }

        private void ApplyPreset(PluginPreset preset) {
            // Apply startup code
            StartupEditor.Text = preset.StartupCode;

            // Write plugin config next to the current HTML file (or CWD if none open)
            string dir = !string.IsNullOrEmpty(_currentFilePath)
                ? System.IO.Path.GetDirectoryName(_currentFilePath)!
                : Directory.GetCurrentDirectory();

            var config = new PluginConfig {
                AssemblyDirectory = preset.AssemblyDirectory,
                Assemblies        = preset.Assemblies,
                Registrar         = preset.Registrar,
                AppSettingsPath   = preset.AppSettingsPath
            };

            string cfgPath = System.IO.Path.Combine(dir, "scryber-plugins.json");
            File.WriteAllText(cfgPath, System.Text.Json.JsonSerializer.Serialize(config,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Reload plugins with the new config
            var (assemblies, pluginPaths, appSettingsPath, asmWarnings) = PluginLoader.LoadAssembliesOnly(_currentFilePath);
            _serviceProvider = CompileStartupServices(StartupEditor.Text, pluginPaths, appSettingsPath);

            string[] regWarnings = [];
            if (_serviceProvider == null && assemblies.Count > 0) {
                var (fallback, rw) = PluginLoader.BuildFromRegistrar(assemblies);
                _serviceProvider = fallback;
                regWarnings = rw;
            }

            var allWarnings = asmWarnings.Concat(regWarnings).ToArray();
            string detail = allWarnings.Length > 0 ? "\n\nWarnings:\n" + string.Join("\n", allWarnings) : "";
            MessageBox.Show($"Preset \"{preset.Name}\" loaded." + detail, "Preset Applied",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ReloadPlugins_Click(object sender, RoutedEventArgs e) {
            var (assemblies, pluginPaths, appSettingsPath, asmWarnings) = PluginLoader.LoadAssembliesOnly(_currentFilePath);

            // Startup tab always takes priority; fall back to convention registrar if it yields nothing
            _serviceProvider = CompileStartupServices(StartupEditor.Text, pluginPaths, appSettingsPath);

            string[] regWarnings = [];
            if (_serviceProvider == null && assemblies.Count > 0) {
                var (fallback, rw) = PluginLoader.BuildFromRegistrar(assemblies);
                _serviceProvider = fallback;
                regWarnings = rw;
            }

            var allWarnings = asmWarnings.Concat(regWarnings).ToArray();
            string status = assemblies.Count > 0
                ? "Plugin assemblies reloaded."
                : "No scryber-plugins.json found — Startup tab services are still active.";
            string detail = allWarnings.Length > 0 ? "\n\nWarnings:\n" + string.Join("\n", allWarnings) : "";
            MessageBox.Show(status + detail, "Plugins", MessageBoxButton.OK, MessageBoxImage.Information);
        }

private ICSharpCode.AvalonEdit.TextEditor ActiveEditor =>
            EditorTabs.SelectedIndex == 0 ? HtmlEditor : ModelEditor;

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FindReplacePanel.Visibility == Visibility.Visible)
                UpdateFindMatches();
        }

        private void OpenFindPanel(bool showReplace) {
            FindReplacePanel.Visibility = Visibility.Visible;
            ReplaceRow.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
            FindBox.Focus();
            FindBox.SelectAll();
            UpdateFindMatches();
        }

        private void CloseFind() {
            FindReplacePanel.Visibility = Visibility.Collapsed;
            _findMatches.Clear();
            _currentMatchIndex = -1;
            MatchCountText.Text = "";
            ActiveEditor.Focus();
        }

        private void CloseFind_Click(object sender, RoutedEventArgs e) => CloseFind();

        private void FindBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateFindMatches();

        private void UpdateFindMatches() {
            _findMatches.Clear();

            string find = FindBox.Text;
            if (string.IsNullOrEmpty(find)) {
                _currentMatchIndex = -1;
                MatchCountText.Text = "";
                return;
            }

            string text = ActiveEditor.Text;
            int index = 0;
            while (true) {
                int pos = text.IndexOf(find, index, StringComparison.OrdinalIgnoreCase);
                if (pos < 0) break;
                _findMatches.Add(pos);
                index = pos + 1;
            }

            if (_findMatches.Count > 0) {
                int caret = ActiveEditor.CaretOffset;
                _currentMatchIndex = _findMatches.FindIndex(m => m >= caret);
                if (_currentMatchIndex < 0) _currentMatchIndex = 0;
                NavigateToMatch(_currentMatchIndex);
            } else {
                _currentMatchIndex = -1;
            }

            UpdateMatchCountText();
        }

        private void UpdateMatchCountText() {
            bool noResults = _findMatches.Count == 0 && FindBox.Text.Length > 0;
            MatchCountText.Text = noResults ? "No results"
                : _findMatches.Count > 0 ? $"{_currentMatchIndex + 1}/{_findMatches.Count}"
                : "";
            MatchCountText.Foreground = noResults
                ? new SolidColorBrush(Color.FromRgb(0xF4, 0x87, 0x71))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void NavigateToMatch(int index) {
            if (index < 0 || index >= _findMatches.Count) return;
            int offset = _findMatches[index];
            ActiveEditor.Select(offset, FindBox.Text.Length);
            ActiveEditor.ScrollToLine(ActiveEditor.Document.GetLineByOffset(offset).LineNumber);
        }

        private void FindNext_Click(object sender, RoutedEventArgs e) => MoveToMatch(1);
        private void FindPrev_Click(object sender, RoutedEventArgs e) => MoveToMatch(-1);

        private void MoveToMatch(int direction) {
            if (_findMatches.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex + direction + _findMatches.Count) % _findMatches.Count;
            NavigateToMatch(_currentMatchIndex);
            UpdateMatchCountText();
        }

        private void FindBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                MoveToMatch(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1);
                e.Handled = true;
            } else if (e.Key == Key.Escape) {
                CloseFind();
                e.Handled = true;
            }
        }

        private void ReplaceBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ReplaceOne_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            } else if (e.Key == Key.Escape) {
                CloseFind();
                e.Handled = true;
            }
        }

        private void ReplaceOne_Click(object sender, RoutedEventArgs e) {
            if (_findMatches.Count == 0 || _currentMatchIndex < 0) return;
            int offset = _findMatches[_currentMatchIndex];
            ActiveEditor.Document.Replace(offset, FindBox.Text.Length, ReplaceBox.Text);
            UpdateFindMatches();
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(FindBox.Text)) return;
            UpdateFindMatches();
            for (int i = _findMatches.Count - 1; i >= 0; i--)
                ActiveEditor.Document.Replace(_findMatches[i], FindBox.Text.Length, ReplaceBox.Text);
            UpdateFindMatches();
        }
    }
}