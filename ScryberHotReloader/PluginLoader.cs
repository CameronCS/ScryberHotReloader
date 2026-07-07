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
        if (_resolverBaseDir == null)
            return null;
        var requested = new AssemblyName(args.Name);

        // Return exact match (name + version) already in the AppDomain.
        // Do NOT return a different version — the CLR validates the manifest and throws
        // 0x80131040 if the returned assembly's version doesn't match what was requested.
        var exact = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => {
            var n = a.GetName();
            return string.Equals(n.Name, requested.Name, StringComparison.OrdinalIgnoreCase)
                && (requested.Version == null || n.Version == requested.Version);
        });
        if (exact != null)
            return exact;

        string dllName = requested.Name + ".dll";

        // 1. User's build output folder — load whatever version is there.
        //    The user controls this directory, and .NET Core's AssemblyResolve
        //    result is not subject to strict manifest-version validation.
        string candidate = Path.Combine(_resolverBaseDir, dllName);
        if (File.Exists(candidate)) {
            try {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            } catch { }
        }

        // 2. Search ALL installed framework versions for the exact requested version.
        //    Keep strict matching here so we never return a mismatched framework assembly
        //    (e.g. avoid returning DI.Abstractions v9 when v10 was requested from .NET 10).
        foreach (string dir in AllFrameworkDirs()) {
            candidate = Path.Combine(dir, dllName);
            if (!File.Exists(candidate))
                continue;
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
        if (_resolverBaseDir != null)
            dirs.Add(_resolverBaseDir);
        dirs.AddRange(SharedFrameworkDirs());
        return [.. dirs];
    }

    // All versions of every shared framework — used by the runtime resolver so it can find
    // an exact version (e.g. DI.Abstractions v10 in .NET 10) even when the hot reloader
    // is running on a different runtime version.
    private static string[] AllFrameworkDirs() {
        if (_allFrameworkDirs != null)
            return _allFrameworkDirs;
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
        if (_sharedFrameworkDirs != null)
            return _sharedFrameworkDirs;

        var dirs = new List<string>();
        try {
            // typeof(object) lives in dotnet/shared/Microsoft.NETCore.App/{version}/
            // Two levels up is the 'shared' folder that also contains AspNetCore.App etc.
            string? currentVersionDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            string? frameworkDir = Path.GetDirectoryName(currentVersionDir);
            string? sharedDir = Path.GetDirectoryName(frameworkDir);

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
                    if (match != null)
                        dirs.Add(match);
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
        if (configPath == null)
            return ([], [], null, []);

        PluginConfig? config = ReadConfig(configPath, warnings);
        if (config == null)
            return ([], [], null, [.. warnings]);

        string? appSettingsPath = config.AppSettingsPath;
        if (config.Assemblies.Count == 0)
            return ([], [], appSettingsPath, [.. warnings]);

        var (assemblies, paths) = LoadAssemblies(config, configPath, warnings);
        return (assemblies, [.. paths], appSettingsPath, [.. warnings]);
    }

    /// <summary>
    /// Builds an IServiceProvider by calling the convention registrar on already-loaded assemblies.
    /// Use as a fallback when the Startup tab is empty.
    ///
    /// Tries two conventions, in order:
    /// 1. A 'ConfigureServices(IServiceCollection)' method (see <see cref="InvokeRegistrar"/>).
    /// 2. A method that builds and returns a host builder (e.g. 'WebApplicationBuilder') — the
    ///    shape most real ASP.NET Core apps actually use (see <see cref="BuildFromWebHostFactory"/>).
    /// </summary>
    public static (IServiceProvider? Provider, string[] Warnings) BuildFromRegistrar(
            List<Assembly> assemblies, string? explicitRegistrar = null, HttpResults? httpResults = null) {
        if (assemblies.Count == 0)
            return (null, []);

        var configureWarnings = new List<string>();
        IServiceCollection services = new ServiceCollection();
        bool registered = InvokeRegistrar(assemblies, services, explicitRegistrar, configureWarnings);

        if (registered) {
            if (httpResults != null)
                services.AddSingleton(httpResults);
            return (services.BuildServiceProvider(), [.. configureWarnings]);
        }

        var (hostProvider, hostWarnings) = BuildFromWebHostFactory(assemblies, explicitRegistrar, httpResults);
        if (hostProvider != null)
            return (hostProvider, [.. hostWarnings]);

        var warnings = new List<string>(configureWarnings);
        warnings.AddRange(hostWarnings);
        warnings.Add($"No '{RegistrarClassName}', 'Program', or 'Startup' class with a " +
                     $"'{ConfigureServicesMethod}(IServiceCollection)' method or a builder-returning " +
                     "factory method (e.g. one returning 'WebApplicationBuilder') was found.");
        return (null, [.. warnings]);
    }

    /// <summary>
    /// Convenience method: loads assemblies and calls the convention registrar in one step.
    /// Preserves the original single-call behaviour for code paths that don't use the Startup tab.
    /// </summary>
    public static (IServiceProvider? Provider, string[] Warnings) Load(string? htmlFilePath) {
        var (assemblies, _, _, asmWarnings) = LoadAssembliesOnly(htmlFilePath);
        if (assemblies.Count == 0)
            return (null, asmWarnings);

        var (provider, regWarnings) = BuildFromRegistrar(assemblies);
        return (provider, [.. asmWarnings, .. regWarnings]);
    }

    // -------------------------------------------------------------------------

    private static string? FindConfig(string? htmlFilePath) {
        if (!string.IsNullOrEmpty(htmlFilePath)) {
            string dir = Path.GetDirectoryName(htmlFilePath) ?? "";
            string candidate = Path.Combine(dir, ConfigFileName);
            if (File.Exists(candidate))
                return candidate;
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

        // Microsoft.Data.SqlClient 5.x defaults to the native SNI networking stack.
        // The native DLL is only on the OS search path when the app ships it — when
        // loaded from a plugin directory it can't be found, causing "not supported on
        // this platform". Switching to the managed stack avoids that entirely.
        AppContext.SetSwitch("Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

        var loaded = new List<Assembly>();
        var paths = new List<string>();

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

    // Public or non-public, static or instance — real-world registrars are often
    // `private static` on an `internal partial class Program`, which the default
    // BindingFlags (public static/instance only) would silently miss.
    private const BindingFlags RegistrarMethodFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static bool InvokeRegistrar(List<Assembly> assemblies, IServiceCollection services, string? explicitRegistrar, List<string> warnings) {
        if (!string.IsNullOrEmpty(explicitRegistrar) && !FindCandidateTypes(assemblies, explicitRegistrar).Any()) {
            warnings.Add($"Registrar type '{explicitRegistrar}' was not found in any loaded assembly.");
            return false;
        }

        var nearMisses = new List<string>();
        foreach (Type candidate in FindCandidateTypes(assemblies, explicitRegistrar)) {
            MethodInfo? method = candidate.GetMethod(ConfigureServicesMethod, RegistrarMethodFlags, [typeof(IServiceCollection)]);
            if (method == null) {
                nearMisses.AddRange(FindNearMisses(candidate));
                continue;
            }

            try {
                object? target = method.IsStatic ? null : Activator.CreateInstance(candidate);
                method.Invoke(target, [services]);
                return true;
            } catch (Exception ex) {
                warnings.Add($"'{ConfigureServicesMethod}' threw an exception: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
        }

        warnings.AddRange(nearMisses);
        return false;
    }

    /// <summary>
    /// Fallback for real-world apps that don't expose a 'ConfigureServices(IServiceCollection)'
    /// method at all — instead they have a method that builds and returns a host builder
    /// (e.g. 'private static WebApplicationBuilder CreateSystemBuilder(string[] args)').
    ///
    /// Invoked entirely via reflection so the hot reloader never needs a compile-time reference
    /// to ASP.NET Core: find the builder-returning method, call it, add HttpResults to its
    /// 'Services' property if present, call '.Build()', and read '.Services' off the result.
    /// Deliberately stops short of '.Run()' or any middleware/hosting setup — only the DI
    /// container is needed for rendering.
    /// </summary>
    private static (IServiceProvider? Provider, string[] Warnings) BuildFromWebHostFactory(
            List<Assembly> assemblies, string? explicitRegistrar, HttpResults? httpResults) {
        var warnings = new List<string>();

        foreach (Type candidate in FindCandidateTypes(assemblies, explicitRegistrar)) {
            MethodInfo? factory = FindBuilderFactory(candidate);
            if (factory == null)
                continue;

            try {
                object?[] args = factory.GetParameters().Length == 1 ? [Array.Empty<string>()] : [];
                object? target = factory.IsStatic ? null : Activator.CreateInstance(candidate);
                object? builder = factory.Invoke(target, args);
                if (builder == null)
                    continue;

                if (httpResults != null &&
                    builder.GetType().GetProperty("Services")?.GetValue(builder) is IServiceCollection builderServices)
                    builderServices.AddSingleton(httpResults);

                MethodInfo? buildMethod = builder.GetType().GetMethod("Build", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                object? host = buildMethod?.Invoke(builder, null);
                if (host == null) {
                    warnings.Add($"'{candidate.FullName}.{factory.Name}' returned a builder with no parameterless 'Build()' method.");
                    continue;
                }

                if (host.GetType().GetProperty("Services")?.GetValue(host) is IServiceProvider provider)
                    return (provider, [.. warnings]);

                warnings.Add($"'{candidate.FullName}.{factory.Name}().Build()' produced a host with no 'Services' property.");
            } catch (Exception ex) {
                warnings.Add($"Failed to build services from '{candidate.FullName}.{factory.Name}': {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        return (null, [.. warnings]);
    }

    // A method that takes no arguments (or just 'string[] args') and returns something whose
    // name looks like a builder (WebApplicationBuilder, HostApplicationBuilder, IHostBuilder...)
    // and that itself exposes a parameterless 'Build()' — duck-typed so no ASP.NET Core
    // reference is required at compile time.
    private static MethodInfo? FindBuilderFactory(Type type) {
        foreach (MethodInfo m in SafeGetMethods(type)) {
            try {
                if (m.ReturnType == typeof(void) || !m.ReturnType.Name.Contains("Builder", StringComparison.OrdinalIgnoreCase))
                    continue;

                ParameterInfo[] ps = m.GetParameters();
                bool paramsOk = ps.Length == 0 || (ps.Length == 1 && ps[0].ParameterType == typeof(string[]));
                if (!paramsOk)
                    continue;

                if (m.ReturnType.GetMethod("Build", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) != null)
                    return m;
            } catch {
                // This method's signature references a type/assembly we can't resolve in this
                // process (common in real-world Program/Startup classes with many unrelated
                // methods) — skip it rather than letting the scan crash.
            }
        }
        return null;
    }

    // Looks for methods that resemble a registration entry point (single parameter that is
    // or looks like an IServiceCollection/builder) but don't match the expected signature —
    // surfaced as a warning so the target project's owner gets an actionable hint instead of
    // a generic "no registrar found".
    private static IEnumerable<string> FindNearMisses(Type type) {
        var results = new List<string>();
        foreach (MethodInfo m in SafeGetMethods(type)) {
            try {
                ParameterInfo[] parameters = m.GetParameters();
                if (parameters.Length != 1)
                    continue;

                string paramName = parameters[0].ParameterType.Name;
                if (paramName.Contains("ServiceCollection") || paramName.Contains("Builder"))
                    results.Add($"Found '{type.FullName}.{m.Name}({paramName})' — " +
                                $"unsupported signature, expected '{ConfigureServicesMethod}(IServiceCollection)'.");
            } catch {
                // Same as above — a signature we can't reflect over is not a near-miss we can
                // report on, so just skip it.
            }
        }
        return results;
    }

    // GetMethods() itself can throw if the type's base type or interfaces reference an
    // unresolvable assembly.
    private static MethodInfo[] SafeGetMethods(Type type) {
        try {
            return type.GetMethods(RegistrarMethodFlags);
        } catch {
            return [];
        }
    }

    // Shared candidate-type resolution for both registrar conventions: an explicit
    // fully-qualified type name if configured, otherwise ScryberPluginRegistrar / Program /
    // Startup (in priority order) across all loaded assemblies.
    private static IEnumerable<Type> FindCandidateTypes(List<Assembly> assemblies, string? explicitRegistrar) {
        if (!string.IsNullOrEmpty(explicitRegistrar)) {
            Type? type = assemblies.Select(a => a.GetType(explicitRegistrar)).FirstOrDefault(t => t != null);
            if (type != null)
                yield return type;
            yield break;
        }

        // Convention priority:
        // 1. ScryberPluginRegistrar   — explicit hot-reloader opt-in
        // 2. Program                  — partial class Program pattern (native, zero new files)
        // 3. Startup                  — classic ASP.NET Core Startup.cs pattern
        string[] candidateNames = [RegistrarClassName, "Program", "Startup"];

        foreach (string name in candidateNames) {
            foreach (Assembly assembly in assemblies) {
                // GetTypes() throws ReflectionTypeLoadException when a transitive dependency
                // DLL is missing (e.g. a mismatched Microsoft.IdentityModel.Tokens version).
                // Fall back to the partial type list so the scan still works.
                Type[] types;
                try {
                    types = assembly.GetTypes();
                } catch (ReflectionTypeLoadException ex) {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                Type? candidate = types.FirstOrDefault(t => t.Name == name);
                if (candidate != null)
                    yield return candidate;
            }
        }
    }
}