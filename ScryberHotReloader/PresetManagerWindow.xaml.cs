using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ScryberHotReloader;

public partial class PresetManagerWindow : Window
{
    private readonly string _currentStartupCode;
    private readonly PluginConfig? _currentPluginConfig;
    private bool _suppressChange;

    public PluginPreset? LoadedPreset { get; private set; }

    public PresetManagerWindow(string currentStartupCode, PluginConfig? currentPluginConfig)
    {
        InitializeComponent();
        _currentStartupCode  = currentStartupCode;
        _currentPluginConfig = currentPluginConfig;
        RefreshList();
    }

    // ── List management ──────────────────────────────────────────────────────

    private void RefreshList(string? selectName = null)
    {
        var presets = PresetManager.LoadAll();
        PresetList.ItemsSource = presets;

        if (selectName != null)
            PresetList.SelectedItem = presets.FirstOrDefault(p => p.Name == selectName);
        else if (presets.Count > 0)
            PresetList.SelectedIndex = 0;
    }

    private void PresetList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var preset = PresetList.SelectedItem as PluginPreset;
        bool hasSelection = preset != null;

        DeleteButton.IsEnabled = hasSelection;
        ExportButton.IsEnabled = hasSelection;
        LoadButton.IsEnabled   = hasSelection;

        if (preset == null) { ClearDetails(); return; }

        _suppressChange = true;
        NameBox.Text          = preset.Name;
        AssemblyDirBox.Text   = preset.AssemblyDirectory ?? "";
        AppSettingsBox.Text   = preset.AppSettingsPath ?? "";
        StartupPreview.Text   = preset.StartupCode;
        _suppressChange = false;
    }

    private void ClearDetails()
    {
        _suppressChange = true;
        NameBox.Text        = "";
        AssemblyDirBox.Text = "";
        AppSettingsBox.Text = "";
        StartupPreview.Text = "";
        _suppressChange = false;
    }

    // ── Detail edits auto-save to the preset ────────────────────────────────

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressChange) return;
        var preset = PresetList.SelectedItem as PluginPreset;
        if (preset == null) return;

        string newName = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == preset.Name) return;

        string oldName = preset.Name;
        preset.Name = newName;
        PresetManager.Delete(oldName);
        PresetManager.Save(preset);
        RefreshList(newName);
    }

    private void DetailsChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressChange) return;
        var preset = PresetList.SelectedItem as PluginPreset;
        if (preset == null) return;
        preset.AssemblyDirectory = string.IsNullOrWhiteSpace(AssemblyDirBox.Text)
            ? null : AssemblyDirBox.Text.Trim();
        PresetManager.Save(preset);
    }

    private void AppSettingsChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressChange) return;
        var preset = PresetList.SelectedItem as PluginPreset;
        if (preset == null) return;
        preset.AppSettingsPath = string.IsNullOrWhiteSpace(AppSettingsBox.Text)
            ? null : AppSettingsBox.Text.Trim();
        PresetManager.Save(preset);
    }

    // ── Buttons ──────────────────────────────────────────────────────────────

    private void NewPreset_Click(object sender, RoutedEventArgs e)
    {
        string name = UniqueName("New Preset");
        var preset  = new PluginPreset { Name = name, StartupCode = Defaults.DefaultStartup };
        PresetManager.Save(preset);
        RefreshList(name);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PluginPreset preset) return;
        if (MessageBox.Show($"Delete preset \"{preset.Name}\"?", "Delete Preset",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        PresetManager.Delete(preset.Name);
        RefreshList();
        ClearDetails();
    }

    private void SaveCurrent_Click(object sender, RoutedEventArgs e)
    {
        string name = InputDialog("Save as Preset", "Preset name:", UniqueName("My Preset"));
        if (string.IsNullOrWhiteSpace(name)) return;

        var preset = new PluginPreset
        {
            Name              = name,
            StartupCode       = _currentStartupCode,
            AssemblyDirectory = _currentPluginConfig?.AssemblyDirectory,
            Assemblies        = _currentPluginConfig?.Assemblies ?? [],
            Registrar         = _currentPluginConfig?.Registrar,
            AppSettingsPath   = _currentPluginConfig?.AppSettingsPath
        };
        PresetManager.Save(preset);
        RefreshList(name);
        StatusText.Text = $"Saved \"{name}\"";
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PluginPreset preset) return;
        LoadedPreset = preset;
        DialogResult = true;
        Close();
    }

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Assembly Directory" };
        if (dlg.ShowDialog() == true)
        {
            AssemblyDirBox.Text = dlg.FolderName;
            var preset = PresetList.SelectedItem as PluginPreset;
            if (preset != null)
            {
                preset.AssemblyDirectory = dlg.FolderName;
                PresetManager.Save(preset);
            }
        }
    }

    private void BrowseAppSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select appsettings.json",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            AppSettingsBox.Text = dlg.FileName;
            var preset = PresetList.SelectedItem as PluginPreset;
            if (preset != null)
            {
                preset.AppSettingsPath = dlg.FileName;
                PresetManager.Save(preset);
            }
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Preset Files (*.scryber-preset.json)|*.scryber-preset.json|JSON Files (*.json)|*.json",
            Title  = "Import Preset"
        };
        if (dlg.ShowDialog() != true) return;

        var preset = PresetManager.Import(dlg.FileName);
        if (preset == null) { MessageBox.Show("Could not read preset file.", "Import Error"); return; }

        preset.Name = UniqueName(preset.Name);
        PresetManager.Save(preset);
        RefreshList(preset.Name);
        StatusText.Text = $"Imported \"{preset.Name}\"";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PluginPreset preset) return;
        var dlg = new SaveFileDialog
        {
            Filter   = "Preset File (*.scryber-preset.json)|*.scryber-preset.json",
            FileName = preset.Name,
            Title    = "Export Preset"
        };
        if (dlg.ShowDialog() != true) return;
        PresetManager.Export(preset, dlg.FileName);
        StatusText.Text = $"Exported to {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    // ── Window chrome ────────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string UniqueName(string baseName)
    {
        var existing = PresetManager.LoadAll().Select(p => p.Name).ToHashSet();
        if (!existing.Contains(baseName)) return baseName;
        int i = 2;
        while (existing.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }

    private static string InputDialog(string title, string prompt, string defaultValue)
    {
        var win = new Window
        {
            Title = title, Width = 340, Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.Transparent,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            ResizeMode = ResizeMode.NoResize
        };

        var border = new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)),
            BorderThickness = new Thickness(1), CornerRadius = new System.Windows.CornerRadius(4)
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        var lbl   = new System.Windows.Controls.TextBlock
            { Text = prompt, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
        var tb = new System.Windows.Controls.TextBox
        {
            Text = defaultValue, SelectionStart = 0, SelectionLength = defaultValue.Length,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x3C, 0x3C, 0x3C)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
            BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4),
            CaretBrush = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var btns = new System.Windows.Controls.StackPanel
            { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new System.Windows.Controls.Button { Content = "OK", Width = 60, Height = 26, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 60, Height = 26 };
        ok.Click     += (_, _) => { win.DialogResult = true;  win.Close(); };
        cancel.Click += (_, _) => { win.DialogResult = false; win.Close(); };
        tb.KeyDown   += (_, e2) => { if (e2.Key == Key.Enter) { win.DialogResult = true; win.Close(); } };

        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        panel.Children.Add(lbl);
        panel.Children.Add(tb);
        panel.Children.Add(btns);
        border.Child = panel;
        win.Content  = border;

        return win.ShowDialog() == true ? tb.Text : "";
    }
}
