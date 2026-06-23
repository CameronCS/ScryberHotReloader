using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace ScryberHotReloader;

public partial class PluginManagerWindow : Window {
    private readonly string _configPath;
    private readonly ObservableCollection<string> _assemblies = [];

    public bool Saved { get; private set; }

    public PluginManagerWindow(string configPath) {
        _configPath = configPath;
        InitializeComponent();
        AssemblyList.ItemsSource = _assemblies;
        LoadExistingConfig();
    }

    private void LoadExistingConfig() {
        if (!File.Exists(_configPath)) return;

        try {
            string json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });

            if (config == null) return;

            AssemblyDirBox.Text  = config.AssemblyDirectory ?? "";
            RegistrarBox.Text    = config.Registrar ?? "";
            AppSettingsBox.Text  = config.AppSettingsPath ?? "";

            foreach (string asm in config.Assemblies)
                _assemblies.Add(asm);
        } catch (Exception ex) {
            StatusText.Text = $"Could not load existing config: {ex.Message}";
        }
    }

    private void BrowseDirectory_Click(object sender, RoutedEventArgs e) {
        var dlg = new OpenFolderDialog {
            Title = "Select Assembly Directory",
            Multiselect = false
        };

        if (dlg.ShowDialog() == true)
            AssemblyDirBox.Text = dlg.FolderName;
    }

    private void BrowseAppSettings_Click(object sender, RoutedEventArgs e) {
        var dlg = new OpenFileDialog {
            Title  = "Select appsettings.json",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            AppSettingsBox.Text = dlg.FileName;
    }

    private void AddAssembly_Click(object sender, RoutedEventArgs e) {
        var dlg = new OpenFileDialog {
            Title = "Select Plugin Assembly",
            Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() != true) return;

        string baseDir = AssemblyDirBox.Text.Trim();

        foreach (string file in dlg.FileNames) {
            // Store relative name when the file lives inside the assembly directory
            string entry = !string.IsNullOrEmpty(baseDir) &&
                           file.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)
                ? System.IO.Path.GetFileName(file)
                : file;

            if (!_assemblies.Contains(entry))
                _assemblies.Add(entry);
        }

        StatusText.Text = "";
    }

    private void RemoveAssembly_Click(object sender, RoutedEventArgs e) {
        if (((System.Windows.Controls.Button)sender).Tag is string entry)
            _assemblies.Remove(entry);
    }

    private void Save_Click(object sender, RoutedEventArgs e) {
        var config = new PluginConfig {
            AssemblyDirectory = AssemblyDirBox.Text.Trim().Length > 0 ? AssemblyDirBox.Text.Trim() : null,
            Assemblies        = [.. _assemblies],
            Registrar         = RegistrarBox.Text.Trim().Length > 0 ? RegistrarBox.Text.Trim() : null,
            AppSettingsPath   = AppSettingsBox.Text.Trim().Length > 0 ? AppSettingsBox.Text.Trim() : null
        };

        try {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json, System.Text.Encoding.UTF8);
            Saved = true;
            Close();
        } catch (Exception ex) {
            StatusText.Text = $"Save failed: {ex.Message}";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
