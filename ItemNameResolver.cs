using D2SLib;
using D2SLib.Model.Save;

namespace D2RItemInspector;

/// <summary>
/// Resolves a fully-qualified display name for an item — unique/set/runeword/magic/rare names
/// plus ethereal and socket annotations — using the game's resource .txt tables.
/// </summary>
public sealed class ItemNameResolver
{
    private readonly Dictionary<int, string> _uniques;
    private readonly Dictionary<int, string> _sets;
    private readonly List<string[]> _runes;
    private readonly List<string[]> _magicPrefixes;
    private readonly List<string[]> _magicSuffixes;
    // Rare names are drawn from a combined index space: suffixes first, then prefixes.
    private readonly List<string> _rareAffixes;

    public ItemNameResolver(string resourceDir)
    {
        string Res(string file) => Path.Combine(resourceDir, file);
        _uniques = LoadById(Res("uniqueitems.txt"));
        _sets = LoadById(Res("setitems.txt"));
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

    private static string BaseName(Item item) =>
        Core.MetaData.ItemsData.GetByCode(item.Code)?["name"].Value ?? item.Code.Trim();

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

    private static string Lookup(Dictionary<int, string> table, int id, string fallback) =>
        table.TryGetValue(id, out var name) ? name : fallback;

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
}
