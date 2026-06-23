using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ScryberHotReloader;

internal static class PluginLoader {
    private const string ConfigFileName = "scryber-plugins.json";
    private const string RegistrarClassName = "ScryberPluginRegistrar";
    private const string ConfigureServicesMethod = "ConfigureServices";

    // Redirects missing dependency lookups to the user's build output folder.
    // Updated each time Load() is called so it always reflects the latest config.
    private static string? _resolverBaseDir;
    private static bool _resolverRegistered;
    private static string[]? _sharedFrameworkDirs;

    private static Assembly? OnAssemblyResolve(object? _, ResolveEventArgs args) {
        if (_resolverBaseDir == null) return null;
        var name = new AssemblyName(args.Name);

        // Return the already-loaded copy to avoid double-load conflicts (e.g. DI.Abstractions
        // already loaded from our NuGet cache, also present in the user's bin folder).
        var existing = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == name.Name);
        if (existing != null) return existing;

        string dllName = name.Name + ".dll";

        // 1. User's build output folder.
        string candidate = Path.Combine(_resolverBaseDir, dllName);
        if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);

        // 2. .NET shared framework directories (AspNetCore.App, WindowsDesktop.App, etc.)
        // Needed when the user's app is framework-dependent and assemblies like
        // Microsoft.Extensions.Hosting.Abstractions live in the shared runtime, not bin.
        foreach (string dir in SharedFrameworkDirs()) {
            candidate = Path.Combine(dir, dllName);
            if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
        }

        return null;
    }

    private static string[] SharedFrameworkDirs() {
        if (_sharedFrameworkDirs != null) return _sharedFrameworkDirs;

        var dirs = new List<string>();
        try {
            // typeof(object) lives in dotnet/shared/Microsoft.NETCore.App/{version}/
            // Two levels up is the 'shared' folder that also contains AspNetCore.App etc.
            string? versionDir  = Path.GetDirectoryName(typeof(object).Assembly.Location);
            string? frameworkDir = Path.GetDirectoryName(versionDir);
            string? sharedDir   = Path.GetDirectoryName(frameworkDir);

            if (sharedDir != null && Directory.Exists(sharedDir)) {
                foreach (string fw in Directory.GetDirectories(sharedDir)) {
                    // All versions, newest first (parse properly to avoid string-sort bugs).
                    foreach (string ver in Directory.GetDirectories(fw)
                        .OrderByDescending(d => {
                            Version.TryParse(Path.GetFileName(d), out var v);
                            return v ?? new Version(0, 0);
                        }))
                        dirs.Add(ver);
                }
            }
        } catch { /* non-critical — resolver just returns null */ }

        _sharedFrameworkDirs = [.. dirs];
        return _sharedFrameworkDirs;
    }

    /// <summary>
    /// Loads plugin assemblies from scryber-plugins.json without calling any registrar.
    /// Call this before compiling Startup tab code so plugin types are available to Roslyn.
    /// </summary>
    public static (List<Assembly> Assemblies, string[] Warnings) LoadAssembliesOnly(string? htmlFilePath) {
        var warnings = new List<string>();

        string? configPath = FindConfig(htmlFilePath);
        if (configPath == null) return ([], []);

        PluginConfig? config = ReadConfig(configPath, warnings);
        if (config == null || config.Assemblies.Count == 0) return ([], [.. warnings]);

        var assemblies = LoadAssemblies(config, configPath, warnings);
        return (assemblies, [.. warnings]);
    }

    /// <summary>
    /// Builds an IServiceProvider by calling the convention registrar on already-loaded assemblies.
    /// Use as a fallback when the Startup tab is empty.
    /// </summary>
    public static (IServiceProvider? Provider, string[] Warnings) BuildFromRegistrar(
            List<Assembly> assemblies, string? explicitRegistrar = null) {
        var warnings = new List<string>();
        if (assemblies.Count == 0) return (null, []);

        IServiceCollection services = new ServiceCollection();
        bool registered = InvokeRegistrar(assemblies, services, explicitRegistrar, warnings);

        if (!registered)
            warnings.Add($"No '{RegistrarClassName}', 'Program', or 'Startup' class with a " +
                         $"'{ConfigureServicesMethod}(IServiceCollection)' method was found.");

        return registered ? (services.BuildServiceProvider(), [.. warnings]) : (null, [.. warnings]);
    }

    /// <summary>
    /// Convenience method: loads assemblies and calls the convention registrar in one step.
    /// Preserves the original single-call behaviour for code paths that don't use the Startup tab.
    /// </summary>
    public static (IServiceProvider? Provider, string[] Warnings) Load(string? htmlFilePath) {
        var (assemblies, asmWarnings) = LoadAssembliesOnly(htmlFilePath);
        if (assemblies.Count == 0) return (null, asmWarnings);

        var (provider, regWarnings) = BuildFromRegistrar(assemblies);
        return (provider, [.. asmWarnings, .. regWarnings]);
    }

    // -------------------------------------------------------------------------

    private static string? FindConfig(string? htmlFilePath) {
        if (!string.IsNullOrEmpty(htmlFilePath)) {
            string dir = Path.GetDirectoryName(htmlFilePath) ?? "";
            string candidate = Path.Combine(dir, ConfigFileName);
            if (File.Exists(candidate)) return candidate;
        }

        string cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }

    private static PluginConfig? ReadConfig(string path, List<string> warnings) {
        try {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
        } catch (Exception ex) {
            warnings.Add($"Failed to read '{path}': {ex.Message}");
            return null;
        }
    }

    private static List<Assembly> LoadAssemblies(PluginConfig config, string configPath, List<string> warnings) {
        string baseDir = !string.IsNullOrEmpty(config.AssemblyDirectory)
            ? config.AssemblyDirectory
            : Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();

        // Point the resolver at this directory so transitive dependencies
        // (EF Core, etc.) are found automatically without listing them in the config.
        _resolverBaseDir = baseDir;
        if (!_resolverRegistered) {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _resolverRegistered = true;
        }

        var loaded = new List<Assembly>();

        foreach (string entry in config.Assemblies) {
            string fullPath = Path.IsPathRooted(entry) ? entry : Path.Combine(baseDir, entry);
            try {
                loaded.Add(Assembly.LoadFrom(fullPath));
            } catch (Exception ex) {
                warnings.Add($"Could not load assembly '{fullPath}': {ex.Message}");
            }
        }

        return loaded;
    }

    private static bool InvokeRegistrar(List<Assembly> assemblies, IServiceCollection services, string? explicitRegistrar, List<string> warnings) {
        MethodInfo? method = null;

        if (!string.IsNullOrEmpty(explicitRegistrar)) {
            // Explicit fully-qualified class name e.g. "MyApp.Business.ScryberPluginRegistrar"
            Type? type = assemblies
                .Select(a => a.GetType(explicitRegistrar))
                .FirstOrDefault(t => t != null);

            if (type == null) {
                warnings.Add($"Registrar type '{explicitRegistrar}' was not found in any loaded assembly.");
                return false;
            }

            method = type.GetMethod(ConfigureServicesMethod, [typeof(IServiceCollection)]);
        } else {
            // Convention priority:
            // 1. ScryberPluginRegistrar   — explicit hot-reloader opt-in
            // 2. Program                  — partial class Program pattern (native, zero new files)
            // 3. Startup                  — classic ASP.NET Core Startup.cs pattern
            string[] candidateNames = [RegistrarClassName, "Program", "Startup"];

            foreach (string name in candidateNames) {
                foreach (Assembly assembly in assemblies) {
                    Type? registrar = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == name);

                    if (registrar == null) continue;

                    method = registrar.GetMethod(ConfigureServicesMethod, [typeof(IServiceCollection)]);
                    if (method != null) break;
                }
                if (method != null) break;
            }
        }

        if (method == null) return false;

        try {
            method.Invoke(null, [services]);
            return true;
        } catch (Exception ex) {
            warnings.Add($"'{ConfigureServicesMethod}' threw an exception: {ex.InnerException?.Message ?? ex.Message}");
            return false;
        }
    }
}
