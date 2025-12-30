using ScryberHotReloader.Models;
using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public interface ICompilationService {
        /// <summary>
        /// Compiles C# source code and instantiates the model
        /// </summary>
        Task<CompilationResult> CompileAndInstantiateModelAsync(string sourceCode);
    }
}
