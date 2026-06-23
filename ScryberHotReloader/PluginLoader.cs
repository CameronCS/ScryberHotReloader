using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
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
    private static string[]? _allFrameworkDirs;

    private static Assembly? OnAssemblyResolve(object? _, ResolveEventArgs args) {
        if (_resolverBaseDir == null) return null;
        var requested = new AssemblyName(args.Name);

        // Return exact match (name + version) already in the AppDomain.
        // Do NOT return a different version — the CLR validates the manifest and throws
        // 0x80131040 if the returned assembly's version doesn't match what was requested.
        var exact = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => {
            var n = a.GetName();
            return string.Equals(n.Name, requested.Name, StringComparison.OrdinalIgnoreCase)
                && (requested.Version == null || n.Version == requested.Version);
        });
        if (exact != null) return exact;

        string dllName = requested.Name + ".dll";

        // 1. User's build output folder — only return it if the version matches what was requested.
        string candidate = Path.Combine(_resolverBaseDir, dllName);
        if (File.Exists(candidate)) {
            try {
                if (requested.Version == null ||
                    AssemblyName.GetAssemblyName(candidate).Version == requested.Version)
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            } catch { }
        }

        // 2. Search ALL installed framework versions for the exact requested version.
        //    This is broader than SharedFrameworkDirs (which is current-version-only for Roslyn)
        //    so we can find e.g. DI.Abstractions v10 in the .NET 10 shared framework even when
        //    the hot reloader itself runs on .NET 9.
        foreach (string dir in AllFrameworkDirs()) {
            candidate = Path.Combine(dir, dllName);
            if (!File.Exists(candidate)) continue;
            try {
                if (requested.Version == null ||
                    AssemblyName.GetAssemblyName(candidate).Version == requested.Version)
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            } catch { }
        }

        return null;
    }

    /// <summary>
    /// Returns directories to scan for Roslyn metadata references:
    /// the user's build output folder and all .NET shared framework directories.
    /// </summary>
    public static string[] GetProbeDirectories() {
        var dirs = new List<string>();
        if (_resolverBaseDir != null) dirs.Add(_resolverBaseDir);
        dirs.AddRange(SharedFrameworkDirs());
        return [.. dirs];
    }

    // All versions of every shared framework — used by the runtime resolver so it can find
    // an exact version (e.g. DI.Abstractions v10 in .NET 10) even when the hot reloader
    // is running on a different runtime version.
    private static string[] AllFrameworkDirs() {
        if (_allFrameworkDirs != null) return _allFrameworkDirs;
        var dirs = new List<string>();
        try {
            string? sharedDir = Path.GetDirectoryName(
                                Path.GetDirectoryName(
                                Path.GetDirectoryName(typeof(object).Assembly.Location)));
            if (sharedDir != null && Directory.Exists(sharedDir)) {
                foreach (string fw in Directory.GetDirectories(sharedDir)) {
                    foreach (string ver in Directory.GetDirectories(fw)
                        .OrderByDescending(d => { Version.TryParse(Path.GetFileName(d), out var v); return v ?? new Version(0, 0); }))
                        dirs.Add(ver);
                }
            }
        } catch { }
        _allFrameworkDirs = [.. dirs];
        return _allFrameworkDirs;
    }

    // Current-runtime-version-only framework dirs — used for Roslyn metadata references
    // to prevent CS0433/CS0518 from conflicting assembly versions.
    private static string[] SharedFrameworkDirs() {
        if (_sharedFrameworkDirs != null) return _sharedFrameworkDirs;

        var dirs = new List<string>();
        try {
            // typeof(object) lives in dotnet/shared/Microsoft.NETCore.App/{version}/
            // Two levels up is the 'shared' folder that also contains AspNetCore.App etc.
            string? currentVersionDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            string? frameworkDir      = Path.GetDirectoryName(currentVersionDir);
            string? sharedDir         = Path.GetDirectoryName(frameworkDir);

            // Only include framework dirs that match our runtime's major.minor — this prevents
            // adding .NET 10 assemblies (DI.Abstractions v10 etc.) when we're running on .NET 9,
            // which would cause version-conflict errors at runtime.
            Version.TryParse(Path.GetFileName(currentVersionDir), out var runtimeVer);

            if (sharedDir != null && runtimeVer != null && Directory.Exists(sharedDir)) {
                foreach (string fw in Directory.GetDirectories(sharedDir)) {
                    string? match = Directory.GetDirectories(fw)
                        .Where(d => Version.TryParse(Path.GetFileName(d), out var v)
                                    && v.Major == runtimeVer.Major && v.Minor == runtimeVer.Minor)
                        .OrderByDescending(d => {
                            Version.TryParse(Path.GetFileName(d), out var v);
                            return v ?? new Version(0, 0);
                        })
                        .FirstOrDefault();
                    if (match != null) dirs.Add(match);
                }
            }
        } catch { /* non-critical — resolver just returns null */ }

        _sharedFrameworkDirs = [.. dirs];
        return _sharedFrameworkDirs;
    }

    /// <summary>
    /// Loads plugin assemblies from scryber-plugins.json without calling any registrar.
    /// Also returns the resolved file paths attempted so Roslyn can reference them directly,
    /// even for assemblies that failed to load at runtime due to missing transitive deps.
    /// </summary>
    public static (List<Assembly> Assemblies, string[] PluginPaths, string? AppSettingsPath, string[] Warnings) LoadAssembliesOnly(string? htmlFilePath) {
        var warnings = new List<string>();

        string? configPath = FindConfig(htmlFilePath);
        if (configPath == null) return ([], [], null, []);

        PluginConfig? config = ReadConfig(configPath, warnings);
        if (config == null) return ([], [], null, [.. warnings]);

        string? appSettingsPath = config.AppSettingsPath;
        if (config.Assemblies.Count == 0) return ([], [], appSettingsPath, [.. warnings]);

        var (assemblies, paths) = LoadAssemblies(config, configPath, warnings);
        return (assemblies, [.. paths], appSettingsPath, [.. warnings]);
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
        var (assemblies, _, _, asmWarnings) = LoadAssembliesOnly(htmlFilePath);
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

    private static (List<Assembly> Loaded, List<string> Paths) LoadAssemblies(
            PluginConfig config, string configPath, List<string> warnings) {
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
        var paths  = new List<string>();

        foreach (string entry in config.Assemblies) {
            string fullPath = Path.IsPathRooted(entry) ? entry : Path.Combine(baseDir, entry);
            paths.Add(fullPath); // track path even if runtime load fails — Roslyn still needs it
            try {
                loaded.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath));
            } catch (Exception ex) {
                warnings.Add($"Could not load assembly '{fullPath}': {ex.Message}");
            }
        }

        return (loaded, paths);
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
