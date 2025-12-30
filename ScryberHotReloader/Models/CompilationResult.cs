namespace ScryberHotReloader.Models {
    public class CompilationResult {
        public bool Success { get; set; }
        public object? ModelInstance { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
