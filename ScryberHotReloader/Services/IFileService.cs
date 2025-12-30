using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public interface IFileService {
        /// <summary>
        /// Opens a file dialog and loads the selected HTML file
        /// </summary>
        Task<(bool Success, string Content, string FilePath)> OpenHtmlFileAsync();

        /// <summary>
        /// Saves content to the specified file path
        /// </summary>
        Task<bool> SaveHtmlFileAsync(string filePath, string content);

        /// <summary>
        /// Opens a save file dialog and saves the content
        /// </summary>
        Task<(bool Success, string FilePath)> SaveHtmlFileAsAsync(string content);

        /// <summary>
        /// Cleans up temporary PDF files
        /// </summary>
        void CleanupTempFile(string filePath);
    }
}
