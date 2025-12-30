using Scryber.Components;
using ScryberHotReloader.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public class PdfService : IPdfService {
        private readonly string _workingDirectory;

        public PdfService() {
            _workingDirectory = Directory.GetCurrentDirectory();
        }

        public async Task<PdfGenerationResult> GeneratePdfAsync(
            string htmlFilePath,
            object? modelInstance,
            ScryberConfiguration config) {

            if (string.IsNullOrEmpty(htmlFilePath)) {
                return new PdfGenerationResult {
                    Success = false,
                    ErrorMessage = "No HTML file path provided"
                };
            }

            try {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                string outputPath = Path.Combine(_workingDirectory, $"preview_{timestamp}.pdf");

                Document document = Document.ParseDocument(htmlFilePath);

                // Set model instance
                if (modelInstance != null) {
                    document.Params["Model"] = modelInstance;
                }

                // Apply configuration settings
                ApplyConfiguration(document, config);

                await document.SaveAsPDFAsync(outputPath);

                return new PdfGenerationResult {
                    Success = true,
                    OutputPath = outputPath
                };
            } catch (Exception ex) {
                return new PdfGenerationResult {
                    Success = false,
                    ErrorMessage = $"Failed to generate PDF:\n{ex.Message}"
                };
            }
        }

        private void ApplyConfiguration(Document document, ScryberConfiguration config) {
            if (config == null) return;

            // Apply page size
            if (!string.IsNullOrEmpty(config.PageSize)) {
                // Page size configuration can be applied here
                // This would require additional Scryber API knowledge
            }

            // Font loading is typically handled through Scryber's font configuration
            // Additional font paths can be configured here if needed
        }
    }
}
