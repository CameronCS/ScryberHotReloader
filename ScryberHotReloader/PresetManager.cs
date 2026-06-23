using System.IO;
using System.Text.Json;

namespace ScryberHotReloader;

internal static class PresetManager
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string PresetsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ScryberHotReloader", "presets");

    public static List<PluginPreset> LoadAll()
    {
        EnsureDirectory();
        var presets = new List<PluginPreset>();
        foreach (var file in Directory.GetFiles(PresetsDirectory, "*.json"))
        {
            try
            {
                var p = JsonSerializer.Deserialize<PluginPreset>(File.ReadAllText(file), _json);
                if (p != null) presets.Add(p);
            }
            catch { /* skip corrupt files */ }
        }
        return [.. presets.OrderBy(p => p.Name)];
    }

    public static void Save(PluginPreset preset)
    {
        EnsureDirectory();
        preset.UpdatedAt = DateTime.UtcNow;
        string path = FilePath(preset.Name);
        File.WriteAllText(path, JsonSerializer.Serialize(preset, _json));
    }

    public static void Delete(string name)
    {
        string path = FilePath(name);
        if (File.Exists(path)) File.Delete(path);
    }

    public static void Rename(string oldName, string newName)
    {
        string oldPath = FilePath(oldName);
        if (!File.Exists(oldPath)) return;
        var preset = JsonSerializer.Deserialize<PluginPreset>(File.ReadAllText(oldPath), _json);
        if (preset == null) return;
        File.Delete(oldPath);
        preset.Name = newName;
        Save(preset);
    }

    public static PluginPreset? Import(string sourcePath)
    {
        try
        {
            var preset = JsonSerializer.Deserialize<PluginPreset>(
                File.ReadAllText(sourcePath), _json);
            return preset;
        }
        catch { return null; }
    }

    public static void Export(PluginPreset preset, string destPath)
    {
        File.WriteAllText(destPath, JsonSerializer.Serialize(preset, _json));
    }

    private static string FilePath(string name) =>
        Path.Combine(PresetsDirectory, SanitiseName(name) + ".json");

    private static string SanitiseName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(PresetsDirectory))
            Directory.CreateDirectory(PresetsDirectory);
    }
}
