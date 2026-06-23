namespace ScryberHotReloader;

public sealed class PluginConfig {
    public string? AssemblyDirectory { get; set; }
    public List<string> Assemblies { get; set; } = [];
    /// <summary>
    /// Fully-qualified class name containing ConfigureServices.
    /// Omit to use the convention: any class named ScryberPluginRegistrar.
    /// </summary>
    public string? Registrar { get; set; }
}
