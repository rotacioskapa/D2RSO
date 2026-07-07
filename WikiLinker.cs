using System.Text.Json;

namespace D2RItemInspector;

/// <summary>
/// Adds diablo.fandom.com wiki links to unique/set/runeword rows. The display name maps to a wiki
/// page (spaces -> underscores; runewords get a " Rune Word" suffix, e.g. Spirit -> Spirit_Rune_Word).
/// Existence is confirmed via the MediaWiki API (batched, so a few
/// requests cover every item), and results are cached to a file so reruns don't touch the network.
/// Best-effort: if the API can't be reached (offline), links are still added unverified so the
/// offline tool keeps working; only pages the API *confirms* are missing get no link.
/// </summary>
public static class WikiLinker
{
    private const string PageBase = "https://diablo.fandom.com/wiki/";
    private const string ApiBase = "https://diablo.fandom.com/api.php";
    private const int BatchSize = 50;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public static void Annotate(List<ReportRow> rows, string cachePath)
    {
        var targets = rows.Where(r => (r.Runeword || r.Rarity is "Unique" or "Set") && !string.IsNullOrWhiteSpace(r.Name)).ToList();
        if (targets.Count == 0) return;

        var cache = LoadCache(cachePath);
        var toCheck = targets.Select(WikiTitle).Distinct(StringComparer.Ordinal)
            .Where(t => !cache.ContainsKey(t)).ToList();

        if (toCheck.Count > 0)
        {
            Console.WriteLine($"Checking {toCheck.Count} wiki link(s)…");
            bool changed = false;
            for (int i = 0; i < toCheck.Count; i += BatchSize)
            {
                try { VerifyBatch(toCheck.GetRange(i, Math.Min(BatchSize, toCheck.Count - i)), cache); changed = true; }
                catch { break; } // offline / API error -> stop; the rest stay unknown (linked unverified)
            }
            if (changed) SaveCache(cachePath, cache);
        }

        // Link unless the wiki confirmed the page is missing (unknown -> still linked, best-effort).
        foreach (var r in targets)
        {
            string title = WikiTitle(r);
            if (!cache.TryGetValue(title, out bool exists) || exists)
                r.WikiUrl = PageBase + title.Replace(' ', '_');
        }
    }

    // Items whose wiki page title differs from the in-game display name (e.g. the wiki misspells the
    // "Ancients' Pledge" runeword as "Ancient's"). Maps display name -> exact wiki page title.
    private static readonly Dictionary<string, string> TitleAliases = new(StringComparer.Ordinal)
    {
        ["Ancients' Pledge"] = "Ancient's Pledge Rune Word",
    };

    // The wiki page title: runewords live at "<Name> Rune Word"; uniques/sets at their name.
    private static string WikiTitle(ReportRow r) =>
        TitleAliases.TryGetValue(r.Name, out var alias) ? alias
        : r.Runeword ? $"{r.Name} Rune Word" : r.Name;

    // One API call for up to 50 titles; marks each as existing (true) or missing (false) in the cache.
    private static void VerifyBatch(List<string> names, Dictionary<string, bool> cache)
    {
        string titles = Uri.EscapeDataString(string.Join("|", names));
        var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}?action=query&format=json&titles={titles}");
        req.Headers.UserAgent.ParseAdd("D2RItemInspector/1.0 (offline item report link check)");
        using var resp = Http.Send(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(resp.Content.ReadAsStream());

        var existing = new HashSet<string>(StringComparer.Ordinal);
        var returned = new HashSet<string>(StringComparer.Ordinal);
        if (doc.RootElement.TryGetProperty("query", out var q) && q.TryGetProperty("pages", out var pages))
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("title", out var t) && t.GetString() is { } title)
                {
                    returned.Add(title);
                    if (page.Value.TryGetProperty("pageid", out var pid) && pid.TryGetInt64(out long id) && id > 0)
                        existing.Add(title);
                }
            }

        foreach (var name in names)
            if (existing.Contains(name)) cache[name] = true;
            else if (returned.Contains(name)) cache[name] = false;
        // Names the API didn't echo back are left uncached (treated as unknown -> linked).
    }

    private static Dictionary<string, bool> LoadCache(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(path))
                    is { } d ? new(d, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        }
        catch { /* corrupt/unreadable cache -> start fresh */ }
        return new(StringComparer.Ordinal);
    }

    private static void SaveCache(string path, Dictionary<string, bool> cache)
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(cache)); }
        catch { /* non-fatal: just means we re-check next run */ }
    }
}
