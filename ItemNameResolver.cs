using D2SLib;
using D2SLib.Model.Save;

namespace D2RItemInspector;

/// <summary>
/// Resolves a fully-qualified display name for an item — unique/set/runeword/magic/rare names
/// plus ethereal and socket annotations — using the game's resource .txt tables.
/// </summary>
public sealed class ItemNameResolver
{
    private readonly StringTable _strings;
    private readonly Dictionary<int, string> _uniques;
    private readonly Dictionary<int, string> _sets;
    private readonly Dictionary<int, string> _setParents; // set-item id -> parent set key
    private readonly List<string[]> _runes;
    private readonly List<string[]> _magicPrefixes;
    private readonly List<string[]> _magicSuffixes;
    // Rare names are drawn from a combined index space: suffixes first, then prefixes.
    private readonly List<string> _rareAffixes;

    public ItemNameResolver(string resourceDir)
    {
        string Res(string file) => Path.Combine(resourceDir, file);
        // Data tables (uniqueitems/setitems "index", base-item code) hold a string Key; the JSON
        // string table maps it to the in-game display name (fixing e.g. "Aldur's Gauntlet").
        _strings = new StringTable(Path.Combine(resourceDir, "strings"), "item-names.json");
        _uniques = LoadById(Res("uniqueitems.txt"));
        _sets = LoadById(Res("setitems.txt"));
        _setParents = LoadIdToCol(Res("setitems.txt"), "ID", "set");
        _runes = LoadTsv(Res("runes.txt"));
        _magicPrefixes = LoadTsv(Res("magicprefix.txt"));
        _magicSuffixes = LoadTsv(Res("magicsuffix.txt"));
        var rarePrefixes = LoadTsv(Res("rareprefix.txt"));
        var rareSuffixes = LoadTsv(Res("raresuffix.txt"));
        _rareAffixes = rareSuffixes.Select(r => r[0]).Concat(rarePrefixes.Select(r => r[0])).ToList();
    }

    public string FullName(Item item)
    {
        if (item.IsEar) return $"Ear of {item.PlayerName}";

        string baseName = BaseName(item);
        string ethereal = item.IsEthereal ? " [Eth]" : "";
        string sockets = item.NumberOfSocketedItems > 0 ? $" <{item.NumberOfSocketedItems} sock>" : "";

        if (item.IsRuneword)
            return $"{RunewordName((int)item.RunewordId)} ({baseName}){ethereal}{sockets}";

        string name = item.Quality switch
        {
            ItemQuality.Unique => $"{Lookup(_uniques, (int)item.FileIndex, baseName)} ({baseName})",
            ItemQuality.Set => $"{Lookup(_sets, (int)item.FileIndex, baseName)} ({baseName})",
            ItemQuality.Magic => JoinNonEmpty(MagicPrefix(item), baseName, MagicSuffix(item)),
            ItemQuality.Rare or ItemQuality.Craft => $"{RareAffix(item.RarePrefixId)} {RareAffix(item.RareSuffixId)} ({baseName})".Trim(),
            _ => baseName,
        };
        return $"{name}{ethereal}{sockets}";
    }

    /// <summary>Base item name (e.g. "Monarch"), ignoring quality/affixes.</summary>
    public string BaseNameOf(Item item) => BaseName(item);

    /// <summary>Clean item name for the report's Name column — no "(base)" suffix, no eth/socket tags.</summary>
    public string DisplayName(Item item)
    {
        if (item.IsEar) return $"Ear of {item.PlayerName}";
        string baseName = BaseName(item);
        if (item.IsRuneword) return RunewordName((int)item.RunewordId);
        return item.Quality switch
        {
            ItemQuality.Unique => Lookup(_uniques, (int)item.FileIndex, baseName),
            ItemQuality.Set => Lookup(_sets, (int)item.FileIndex, baseName),
            ItemQuality.Magic => JoinNonEmpty(MagicPrefix(item), baseName, MagicSuffix(item)),
            ItemQuality.Rare or ItemQuality.Craft => $"{RareAffix(item.RarePrefixId)} {RareAffix(item.RareSuffixId)}".Trim(),
            ItemQuality.Superior => $"Superior {baseName}",
            ItemQuality.Inferior => $"{InferiorPrefix((int)item.FileIndex)} {baseName}".Trim(),
            _ => baseName,
        };
    }

    /// <summary>Runeword name if this item is a runeword, else null.</summary>
    public string? RunewordNameOf(Item item) => item.IsRuneword ? RunewordName((int)item.RunewordId) : null;

    /// <summary>Parent-set name if this item is a set item (e.g. "Cathan's Traps"), else null.</summary>
    public string? SetNameOf(Item item) =>
        SetKeyOf(item) is { } key ? (_strings.Get(key) ?? key) : null;

    /// <summary>Parent-set key (sets.txt index) of a set item, for looking up whole-set bonuses.</summary>
    public string? SetKeyOf(Item item) =>
        item.Quality == ItemQuality.Set && _setParents.TryGetValue((int)item.FileIndex, out var key) ? key : null;

    // Low-quality (inferior) prefix by subtype index. Best-effort standard D2 set; RotW data may differ.
    private static string InferiorPrefix(int fileIndex) => fileIndex switch
    {
        0 => "Crude",
        1 => "Cracked",
        2 => "Damaged",
        _ => "Low Quality",
    };

    private string BaseName(Item item)
    {
        string code = item.Code.Trim();
        return _strings.Get(code)
            ?? Core.MetaData.ItemsData.GetByCode(item.Code)?["name"].Value
            ?? code;
    }

    private string RunewordName(int runewordId)
    {
        int index = runewordId - 27;
        return index >= 0 && index < _runes.Count ? _runes[index][1] : $"Runeword?{runewordId}";
    }

    private string MagicPrefix(Item item) => Affix(_magicPrefixes, item.MagicPrefixIds[0]);
    private string MagicSuffix(Item item) => Affix(_magicSuffixes, item.MagicSuffixIds[0]);

    private string RareAffix(int id) =>
        id >= 1 && id <= _rareAffixes.Count ? Capitalize(_rareAffixes[id - 1]) : "";

    private static string Affix(List<string[]> table, int id) =>
        id >= 1 && id <= table.Count ? table[id - 1][0] : "";

    // Resolve a unique/set id -> its string Key -> the in-game display name.
    private string Lookup(Dictionary<int, string> table, int id, string fallback) =>
        table.TryGetValue(id, out var key) ? (_strings.Get(key) ?? key) : fallback;

    private static string JoinNonEmpty(params string[] parts) =>
        string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Capitalize(string word) =>
        string.IsNullOrEmpty(word) ? word : char.ToUpper(word[0]) + word[1..];

    private static List<string[]> LoadTsv(string file) =>
        File.ReadAllLines(file).Skip(1).Select(line => line.Split('\t')).ToList();

    private static Dictionary<int, string> LoadById(string file)
    {
        var map = new Dictionary<int, string>();
        foreach (var row in LoadTsv(file))
            if (row.Length > 1 && int.TryParse(row[1], out int id) && !map.ContainsKey(id))
                map[id] = row[0];
        return map;
    }

    // Maps a numeric id column to a string column (both by header name; "*" prefix tolerated).
    private static Dictionary<int, string> LoadIdToCol(string file, string idCol, string valCol)
    {
        var map = new Dictionary<int, string>();
        var lines = File.ReadAllLines(file);
        if (lines.Length == 0) return map;
        var header = lines[0].Split('\t');
        int ii = Array.FindIndex(header, h => h.TrimStart('*').Trim() == idCol);
        int vi = Array.FindIndex(header, h => h.Trim() == valCol);
        if (ii < 0 || vi < 0) return map;
        foreach (var line in lines.Skip(1))
        {
            var c = line.Split('\t');
            if (ii < c.Length && vi < c.Length && int.TryParse(c[ii], out int id))
                map.TryAdd(id, c[vi]);
        }
        return map;
    }
}
