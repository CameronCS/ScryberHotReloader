using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ScryberHotReloader.Services {
    public class FileService : IFileService {
        public async Task<(bool Success, string Content, string FilePath)> OpenHtmlFileAsync() {
            OpenFileDialog dlg = new() {
                Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*",
                Title = "Open HTML File"
            };

            if (dlg.ShowDialog() == true) {
                try {
                    string content = await File.ReadAllTextAsync(dlg.FileName, Encoding.UTF8);
                    return (true, content, dlg.FileName);
                } catch (Exception ex) {
                    MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return (false, string.Empty, string.Empty);
                }
            }

            return (false, string.Empty, string.Empty);
        }

        public async Task<bool> SaveHtmlFileAsync(string filePath, string content) {
            if (string.IsNullOrEmpty(filePath)) {
                return false;
            }

            try {
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
                return true;
            } catch (Exception ex) {
                MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<(bool Success, string FilePath)> SaveHtmlFileAsAsync(string content) {
            SaveFileDialog dlg = new() {
                Filter = "HTML Files (*.html)|*.html|All files (*.*)|*.*",
                Title = "Save HTML File As"
            };

            if (dlg.ShowDialog() == true) {
                bool success = await SaveHtmlFileAsync(dlg.FileName, content);
                return (success, dlg.FileName);
            }

            return (false, string.Empty);
        }

        public void CleanupTempFile(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return;
            }

            try {
                File.Delete(filePath);
            } catch {
                // Ignore errors silently
            }
        }
    }
}
