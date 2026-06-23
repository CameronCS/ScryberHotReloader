namespace ScryberHotReloader;

public sealed class PluginPreset
{
    public string Name            { get; set; } = "New Preset";
    public string? AssemblyDirectory { get; set; }
    public List<string> Assemblies   { get; set; } = [];
    public string? Registrar         { get; set; }
    public string? AppSettingsPath   { get; set; }
    public string StartupCode        { get; set; } = Defaults.DefaultStartup;
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt        { get; set; } = DateTime.UtcNow;
}
