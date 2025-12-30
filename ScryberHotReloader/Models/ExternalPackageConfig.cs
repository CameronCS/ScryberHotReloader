using System.Collections.Generic;

namespace ScryberHotReloader.Models {
    public class ExternalPackageConfig {
        /// <summary>
        /// List of assembly file paths to load
        /// </summary>
        public List<string> AssemblyPaths { get; set; } = new();

        /// <summary>
        /// List of NuGet package names to load
        /// </summary>
        public List<string> NuGetPackages { get; set; } = new();
    }
}
