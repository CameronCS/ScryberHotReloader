namespace ScryberHotReloader.Models {
    public class PdfGenerationResult {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
