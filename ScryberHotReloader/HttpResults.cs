using System.Text.Json;

namespace ScryberHotReloader;

/// <summary>
/// Holds raw JSON responses from the HTTP tab. Inject this in a runner constructor
/// to read HTTP data: httpResults.Get&lt;MyDto&gt;("requestName")
/// </summary>
public sealed class HttpResults {
    private readonly Dictionary<string, string> _raw;

    public HttpResults(Dictionary<string, string> raw) => _raw = raw;

    public string? GetJson(string name) =>
        _raw.TryGetValue(name, out var v) ? v : null;

    public T? Get<T>(string name) {
        if (!_raw.TryGetValue(name, out var json))
            return default;
        try {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        } catch { return default; }
    }

    public bool Has(string name) => _raw.ContainsKey(name);

    public IReadOnlyDictionary<string, string> All => _raw;
}