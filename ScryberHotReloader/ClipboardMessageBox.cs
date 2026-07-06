using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScryberHotReloader;

/// <summary>
/// Dark-themed message box that includes a "Copy to Clipboard" button.
/// Drop-in replacement for MessageBox.Show — same call signature, void return.
/// </summary>
internal static class ClipboardMessageBox {
    public static void Show(
        string message,
        string title = "Error",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.Information) {
        var win = new Window {
            Title = title,
            Width = 560,
            MaxHeight = 620,
            MinHeight = 140,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
        };

        var outer = new DockPanel { Margin = new Thickness(16) };

        // Button row docked to the bottom
        var btnRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        DockPanel.SetDock(btnRow, Dock.Bottom);
        outer.Children.Add(btnRow);

        // Icon, if the caller asked for one (matches MessageBox's icon-by-severity convention)
        var iconSource = GetIconSource(icon);
        if (iconSource != null) {
            var iconImage = new Image {
                Source = iconSource,
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            DockPanel.SetDock(iconImage, Dock.Top);
            outer.Children.Add(iconImage);
        }

        // Scrollable read-only text area (monospace so stack traces align)
        var tb = new TextBox {
            Text = message,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            BorderThickness = new Thickness(1),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(8),
            MaxHeight = 440,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        outer.Children.Add(tb);

        var copyBtn = new Button {
            Content = "Copy to Clipboard",
            Padding = new Thickness(14, 5, 14, 5),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
        };
        copyBtn.Click += (_, _) => {
            Clipboard.SetText(message);
            copyBtn.Content = "Copied!";
        };

        var okBtn = new Button {
            Content = "OK",
            Padding = new Thickness(24, 5, 24, 5),
            MinWidth = 72,
            IsDefault = true,
            Cursor = Cursors.Hand,
        };
        okBtn.Click += (_, _) => win.Close();

        btnRow.Children.Add(copyBtn);
        btnRow.Children.Add(okBtn);

        win.Content = outer;
        win.ShowDialog();
    }

    private static BitmapSource? GetIconSource(MessageBoxImage icon) {
        System.Drawing.Icon? sysIcon = icon switch {
            MessageBoxImage.Error => System.Drawing.SystemIcons.Error,
            MessageBoxImage.Warning => System.Drawing.SystemIcons.Warning,
            MessageBoxImage.Question => System.Drawing.SystemIcons.Question,
            MessageBoxImage.Information => System.Drawing.SystemIcons.Information,
            _ => null,
        };
        if (sysIcon == null)
            return null;

        return Imaging.CreateBitmapSourceFromHIcon(
            sysIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
    }
}