using System.Text.Json;

namespace D2RItemInspector;

/// <summary>
/// Loads D2R JSON string tables (data/local/lng/strings/*.json) into a Key → enUS lookup.
/// Data tables store the string Key in their "index"/code columns; this resolves those to the
/// in-game display text (e.g. set-item key "Aldur's Gauntlet" → "Aldur's Rhythm").
/// </summary>
public sealed class StringTable
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public StringTable(string stringsDir, params string[] files)
    {
        // D2R JSON files use a BOM (handled by ReadAllText) and trailing commas.
        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        foreach (string file in files)
        {
            string path = Path.Combine(stringsDir, file);
            if (!File.Exists(path)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path), options);
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (entry.TryGetProperty("Key", out var k) && k.GetString() is { Length: > 0 } key
                    && entry.TryGetProperty("enUS", out var v))
                    _map.TryAdd(key, v.GetString() ?? "");
            }
        }
    }

    public string? Get(string key) => _map.TryGetValue(key, out var value) ? value : null;
}
