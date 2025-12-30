using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public interface IExternalPackageService {
        /// <summary>
        /// Loads external assemblies from configuration
        /// </summary>
        Task LoadExternalPackagesAsync();

        /// <summary>
        /// Gets metadata references for external packages
        /// </summary>
        IEnumerable<MetadataReference> GetMetadataReferences();

        /// <summary>
        /// Gets the list of loaded external assemblies
        /// </summary>
        IEnumerable<string> GetLoadedAssemblies();
    }
}
