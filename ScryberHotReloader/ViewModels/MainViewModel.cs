using ScryberHotReloader.Models;
using ScryberHotReloader.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace ScryberHotReloader.ViewModels {
    public class MainViewModel : ViewModelBase {
        private readonly IFileService _fileService;
        private readonly ICompilationService _compilationService;
        private readonly IPdfService _pdfService;
        private readonly IExternalPackageService _externalPackageService;

        private string _htmlContent = "";
        private string _modelContent = "";
        private string _currentFilePath = "";
        private string _previousPdfPath = "";
        private ScryberConfiguration _configuration = new();

        public MainViewModel(
            IFileService fileService,
            ICompilationService compilationService,
            IPdfService pdfService,
            IExternalPackageService externalPackageService) {

            _fileService = fileService;
            _compilationService = compilationService;
            _pdfService = pdfService;
            _externalPackageService = externalPackageService;

            Configuration = new ScryberConfiguration();
            LoadedAssemblies = new ObservableCollection<string>();

            // Load external packages on startup
            _ = LoadExternalPackagesAsync();
        }

        public string HtmlContent {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        public string ModelContent {
            get => _modelContent;
            set => SetProperty(ref _modelContent, value);
        }

        public string CurrentFilePath {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        public ScryberConfiguration Configuration {
            get => _configuration;
            set => SetProperty(ref _configuration, value);
        }

        public ObservableCollection<string> LoadedAssemblies { get; }

        public async Task OpenHtmlFileAsync() {
            var result = await _fileService.OpenHtmlFileAsync();
            if (result.Success) {
                HtmlContent = result.Content;
                CurrentFilePath = result.FilePath;
                await SaveAndGeneratePdfAsync();
            }
        }

        public async Task<bool> SaveHtmlFileAsync() {
            if (string.IsNullOrEmpty(CurrentFilePath)) {
                return await SaveHtmlFileAsAsync();
            }

            return await _fileService.SaveHtmlFileAsync(CurrentFilePath, HtmlContent);
        }

        public async Task<bool> SaveHtmlFileAsAsync() {
            var result = await _fileService.SaveHtmlFileAsAsync(HtmlContent);
            if (result.Success) {
                CurrentFilePath = result.FilePath;
                return true;
            }
            return false;
        }

        public async Task SaveAndGeneratePdfAsync() {
            // Save HTML file
            if (string.IsNullOrEmpty(CurrentFilePath)) {
                var result = await _fileService.SaveHtmlFileAsAsync(HtmlContent);
                if (!result.Success) {
                    return;
                }
                CurrentFilePath = result.FilePath;
            } else {
                await _fileService.SaveHtmlFileAsync(CurrentFilePath, HtmlContent);
            }

            // Compile model
            var compilationResult = await _compilationService.CompileAndInstantiateModelAsync(ModelContent);
            if (!compilationResult.Success) {
                MessageBox.Show(compilationResult.ErrorMessage, "Compilation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Generate PDF
            var pdfResult = await _pdfService.GeneratePdfAsync(
                CurrentFilePath,
                compilationResult.ModelInstance,
                Configuration
            );

            if (!pdfResult.Success) {
                MessageBox.Show(pdfResult.ErrorMessage, "PDF Generation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Clean up previous PDF
            if (!string.IsNullOrEmpty(_previousPdfPath)) {
                _fileService.CleanupTempFile(_previousPdfPath);
            }

            _previousPdfPath = pdfResult.OutputPath ?? "";

            // Notify that PDF is ready (MainWindow will handle the UI update)
            OnPdfGenerated(pdfResult.OutputPath);
        }

        public void Cleanup() {
            if (!string.IsNullOrEmpty(_previousPdfPath)) {
                _fileService.CleanupTempFile(_previousPdfPath);
            }
        }

        private async Task LoadExternalPackagesAsync() {
            try {
                await _externalPackageService.LoadExternalPackagesAsync();
                var assemblies = _externalPackageService.GetLoadedAssemblies();
                foreach (var assembly in assemblies) {
                    LoadedAssemblies.Add(assembly);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error loading external packages: {ex.Message}");
            }
        }

        // Event for PDF generation
        public event EventHandler<string?>? PdfGenerated;

        protected virtual void OnPdfGenerated(string? pdfPath) {
            PdfGenerated?.Invoke(this, pdfPath);
        }
    }
}
