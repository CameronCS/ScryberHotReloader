using ScryberHotReloader.Models;
using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public interface IPdfService {
        /// <summary>
        /// Generates a PDF from HTML file and model instance
        /// </summary>
        Task<PdfGenerationResult> GeneratePdfAsync(string htmlFilePath, object? modelInstance, ScryberConfiguration config);
    }
}
