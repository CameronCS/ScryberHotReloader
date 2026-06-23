using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ScryberHotReloader;

internal static class PluginLoader {
    private const string ConfigFileName = "scryber-plugins.json";
    private const string RegistrarClassName = "ScryberPluginRegistrar";
    private const string ConfigureServicesMethod = "ConfigureServices";

    /// <summary>
    /// Locates scryber-plugins.json, loads the listed assemblies, calls the registrar,
    /// and returns the built service provider plus any non-fatal warnings.
    /// Returns a null provider when no config file is found — existing behaviour is preserved.
    /// </summary>
    public static (IServiceProvider? Provider, string[] Warnings) Load(string? htmlFilePath) {
        var warnings = new List<string>();

        string? configPath = FindConfig(htmlFilePath);
        if (configPath == null)
            return (null, []);

        PluginConfig? config = ReadConfig(configPath, warnings);
        if (config == null || config.Assemblies.Count == 0)
            return (null, [.. warnings]);

        List<Assembly> assemblies = LoadAssemblies(config, configPath, warnings);
        if (assemblies.Count == 0)
            return (null, [.. warnings]);

        IServiceCollection services = new ServiceCollection();
        bool registered = InvokeRegistrar(assemblies, services, config.Registrar, warnings);

        if (!registered)
            warnings.Add($"No '{RegistrarClassName}' class with a '{ConfigureServicesMethod}(IServiceCollection)' method was found in the loaded assemblies. Services will not be injected.");

        return (services.BuildServiceProvider(), [.. warnings]);
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

        var loaded = new List<Assembly>();

        foreach (string entry in config.Assemblies) {
            string fullPath = Path.IsPathRooted(entry) ? entry : Path.Combine(baseDir, entry);
            try {
                // LoadFrom registers the assembly in the AppDomain — Roslyn picks it up
                // automatically when scanning AppDomain.CurrentDomain.GetAssemblies().
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
            // Convention: find any class named ScryberPluginRegistrar
            foreach (Assembly assembly in assemblies) {
                Type? registrar = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == RegistrarClassName);

                if (registrar == null) continue;

                method = registrar.GetMethod(ConfigureServicesMethod, [typeof(IServiceCollection)]);
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
