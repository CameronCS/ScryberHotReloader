using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using ScryberHotReloader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public class ExternalPackageService : IExternalPackageService {
        private readonly List<Assembly> _loadedAssemblies = new();
        private readonly List<MetadataReference> _metadataReferences = new();
        private readonly IConfiguration _configuration;

        public ExternalPackageService(IConfiguration configuration) {
            _configuration = configuration;
        }

        public async Task LoadExternalPackagesAsync() {
            await Task.Run(() => {
                var packageConfig = _configuration.GetSection("ExternalPackages").Get<ExternalPackageConfig>();

                if (packageConfig == null || packageConfig.AssemblyPaths == null) {
                    return;
                }

                foreach (var assemblyPath in packageConfig.AssemblyPaths) {
                    try {
                        // Support both absolute and relative paths
                        string fullPath = Path.IsPathRooted(assemblyPath)
                            ? assemblyPath
                            : Path.Combine(Directory.GetCurrentDirectory(), assemblyPath);

                        if (!File.Exists(fullPath)) {
                            Console.WriteLine($"Warning: Assembly not found: {fullPath}");
                            continue;
                        }

                        Assembly assembly = Assembly.LoadFrom(fullPath);
                        _loadedAssemblies.Add(assembly);
                        _metadataReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                    } catch (Exception ex) {
                        Console.WriteLine($"Error loading assembly {assemblyPath}: {ex.Message}");
                    }
                }

                // Load NuGet packages if specified
                if (packageConfig.NuGetPackages != null) {
                    foreach (var package in packageConfig.NuGetPackages) {
                        try {
                            LoadNuGetPackage(package);
                        } catch (Exception ex) {
                            Console.WriteLine($"Error loading NuGet package {package}: {ex.Message}");
                        }
                    }
                }
            });
        }

        private void LoadNuGetPackage(string packageName) {
            // Try to find the package in common NuGet locations
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var nugetPath = Path.Combine(userProfile, ".nuget", "packages");

            if (!Directory.Exists(nugetPath)) {
                return;
            }

            var packagePath = Path.Combine(nugetPath, packageName.ToLower());
            if (!Directory.Exists(packagePath)) {
                return;
            }

            // Find the latest version
            var versions = Directory.GetDirectories(packagePath);
            if (versions.Length == 0) {
                return;
            }

            var latestVersion = versions.OrderByDescending(v => v).First();

            // Look for lib folders
            var libPath = Path.Combine(latestVersion, "lib");
            if (!Directory.Exists(libPath)) {
                return;
            }

            // Find appropriate framework folder (prefer .NET 9, then 8, then 7, etc.)
            var frameworkFolders = Directory.GetDirectories(libPath)
                .Where(d => {
                    var folderName = Path.GetFileName(d).ToLower();
                    return folderName.StartsWith("net9.0") ||
                           folderName.StartsWith("net8.0") ||
                           folderName.StartsWith("net7.0") ||
                           folderName.StartsWith("netstandard2");
                })
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (frameworkFolders == null) {
                return;
            }

            // Load all DLLs in the folder
            var dlls = Directory.GetFiles(frameworkFolders, "*.dll");
            foreach (var dll in dlls) {
                try {
                    Assembly assembly = Assembly.LoadFrom(dll);
                    _loadedAssemblies.Add(assembly);
                    _metadataReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                } catch {
                    // Ignore if DLL can't be loaded
                }
            }
        }

        public IEnumerable<MetadataReference> GetMetadataReferences() {
            return _metadataReferences;
        }

        public IEnumerable<string> GetLoadedAssemblies() {
            return _loadedAssemblies.Select(a => a.FullName ?? "Unknown");
        }
    }
}
