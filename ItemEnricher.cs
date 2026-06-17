using D2SLib;
using D2SLib.Model.Data;
using D2SLib.Model.Save;

namespace D2RItemInspector;

/// <summary>
/// Derives the rich report fields for an item (type/base/tier, rarity/color, required level/str/dex,
/// class restriction, sockets, eth) from the parsed <see cref="Item"/> and the game data tables.
/// Required level isn't stored in the save, so it's computed from the same inputs the game uses.
/// </summary>
public sealed class ItemEnricher
{
    private readonly ItemNameResolver _names;
    private readonly Dictionary<int, int> _uniqueLvlReq; // unique *ID -> required level
    private readonly Dictionary<int, int> _setLvlReq;    // set *ID -> required level
    private readonly List<int> _magicPrefixLvlReq;       // affix id -> required level (1-based)
    private readonly List<int> _magicSuffixLvlReq;
    private readonly Dictionary<int, string> _skillNames; // skill *Id -> name
    private readonly string[][] _skillTabs;               // [classId][localTab 0-2] -> tree name

    // Class codes (classId order) used by the SkillCategory<code><1-3> string keys.
    private static readonly string[] ClassCodes = { "Am", "So", "Ne", "Pa", "Ba", "Dr", "As", "Wa" };

    public ItemEnricher(ItemNameResolver names, string resourceDir)
    {
        _names = names;
        string Res(string f) => Path.Combine(resourceDir, f);
        _uniqueLvlReq = LoadKeyValMap(Res("uniqueitems.txt"), "ID", "lvl req");
        _setLvlReq = LoadKeyValMap(Res("setitems.txt"), "ID", "lvl req");
        _magicPrefixLvlReq = LoadColumnInts(Res("magicprefix.txt"), "levelreq");
        _magicSuffixLvlReq = LoadColumnInts(Res("magicsuffix.txt"), "levelreq");
        _skillNames = LoadSkillNames(Res("skills.txt"));
        _skillTabs = BuildSkillTabs(new StringTable(Path.Combine(resourceDir, "strings"), "skills.json"));
        _setBonuses = new SetBonusResolver(Res("sets.txt"), SkillName);
    }

    private readonly SetBonusResolver _setBonuses;

    private string? SkillName(int id) =>
        _skillNames.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;

    // Skill-tree names per class, from the SkillCategory<code><1-3> string keys (localTab 0 -> "...1").
    private static string[][] BuildSkillTabs(StringTable strings)
    {
        var tabs = new string[ClassCodes.Length][];
        for (int c = 0; c < ClassCodes.Length; c++)
        {
            tabs[c] = new string[3];
            for (int t = 0; t < 3; t++)
                tabs[c][t] = strings.Get($"SkillCategory{ClassCodes[c]}{t + 1}") ?? "Skill";
        }
        return tabs;
    }

    public void Enrich(Item item, ItemData data)
    {
        data.Ethereal = item.IsEthereal;
        data.Equipped = item.Mode == ItemMode.Equipped;
        data.IsRuneword = item.IsRuneword;
        data.RunewordName = _names.RunewordNameOf(item);
        data.SetName = _names.SetNameOf(item);
        if (_names.SetKeyOf(item) is { } setKey && _setBonuses.Get(setKey) is { } sb)
            data.SetBonuses = sb;
        data.DisplayName = _names.DisplayName(item);
        data.BaseName = _names.BaseNameOf(item);
        data.Rarity = MapRarity(item.Quality);
        data.Stats = StatFormatter.Format(item, SkillName, _skillTabs);
        data.SocketCount = item.IsSocketed && item.TotalNumberOfSockets > 0
            ? item.TotalNumberOfSockets : item.NumberOfSocketedItems;
        data.ColorClass = ComputeColor(data);

        var (file, row) = BaseRow(item.Code);
        if (file is null || row is null) return; // not in tables (e.g. ear) -> stays excluded

        string code = item.Code.Trim();
        string type = GetStr(file, row, "type");
        data.TypeCategory = Categorize(code, type);
        data.AllowedClass = ClassRestriction(type);
        data.BaseQuality = Tier(file, row, code);
        data.RequiredStrength = GetInt(file, row, "reqstr");
        data.RequiredDexterity = GetInt(file, row, "reqdex");
        data.RequiredLevel = ComputeReqLevel(item, file, row);
        AddDefenseOrDamage(item, data, file, row);
    }

    // Prepend the item's defense (armor) or damage range (weapons) to the tooltip, like the
    // in-game header line above the magical properties.
    private static void AddDefenseOrDamage(Item item, ItemData data, DataFile file, DataRow row)
    {
        var items = Core.MetaData.ItemsData;
        string code = item.Code.Trim();
        if (items.IsArmor(code) && item.Armor > 0)
        {
            data.Stats.Insert(0, new StatLine { Text = $"Defense: {item.Armor}" });
            return;
        }
        if (!items.IsWeapon(code)) return;

        int ed = StatVal(item, "item_mindamage_percent");
        int addMin = StatVal(item, "mindamage"), addMax = StatVal(item, "maxdamage");
        string? oneH = DamageRange(file, row, "mindam", "maxdam", ed, addMin, addMax);
        string? twoH = DamageRange(file, row, "2handmindam", "2handmaxdam", ed, addMin, addMax);
        if (oneH is not null && twoH is not null)
        {
            data.Stats.Insert(0, new StatLine { Text = $"Two-Hand Damage: {twoH}" });
            data.Stats.Insert(0, new StatLine { Text = $"One-Hand Damage: {oneH}" });
        }
        else if ((oneH ?? twoH) is { } dmg)
            data.Stats.Insert(0, new StatLine { Text = $"Damage: {dmg}" });
    }

    private static int StatVal(Item item, string stat) =>
        item.StatLists.SelectMany(l => l.Stats).FirstOrDefault(s => s.Stat == stat)?.Value ?? 0;

    // In-game damage = base * (100 + ED%) / 100 + flat added min/max.
    private static string? DamageRange(DataFile file, DataRow row, string minCol, string maxCol, int ed, int addMin, int addMax)
    {
        int bMin = GetInt(file, row, minCol), bMax = GetInt(file, row, maxCol);
        if (bMin == 0 && bMax == 0) return null;
        int min = bMin * (100 + ed) / 100 + addMin;
        int max = bMax * (100 + ed) / 100 + addMax;
        return $"{min}-{max}";
    }

    // ---- classification ----

    private static string? Categorize(string code, string type)
    {
        var items = Core.MetaData.ItemsData;
        if (items.IsWeapon(code)) return WeaponCategory(type);
        if (items.IsArmor(code))
            return type switch
            {
                "helm" or "phlm" or "pelt" or "circ" => "Helm",
                "tors" => "Body Armor",
                "glov" => "Gloves",
                "belt" => "Belt",
                "boot" => "Boots",
                "shie" or "ashd" or "head" or "grim" => "Shield",
                _ => "Armor",
            };
        return type switch
        {
            "amul" => "Amulet",
            "ring" => "Ring",
            "scha" or "mcha" or "lcha" => "Charm",
            "jewl" => "Jewel",
            _ => null, // gems, runes, potions, keys, quivers, etc. -> excluded
        };
    }

    // Weapon `type` code -> weapon class (so "Type" distinguishes Sword/Mace/Polearm/etc.).
    private static string? WeaponCategory(string type) => type switch
    {
        "swor" => "Sword",
        "axe" => "Axe",
        "mace" or "club" or "hamm" => "Mace",
        "scep" => "Scepter",
        "pole" => "Polearm",
        "spea" or "aspe" => "Spear",
        "jave" or "ajav" => "Javelin",
        "bow" or "abow" => "Bow",
        "xbow" => "Crossbow",
        "knif" => "Dagger",
        "wand" => "Wand",
        "staf" => "Staff",
        "orb" => "Orb",
        "h2h" or "h2h2" => "Claw",
        "taxe" or "tkni" => "Throwing",
        "tpot" => null, // throwing potions live in weapons.txt but are consumables -> exclude
        _ => "Weapon",
    };

    private static string? ClassRestriction(string type) => type switch
    {
        "pelt" => "Druid",
        "phlm" => "Barbarian",
        "head" => "Necromancer",
        "ashd" => "Paladin",
        "orb" => "Sorceress",
        "h2h" or "h2h2" => "Assassin",
        "abow" or "aspe" or "ajav" => "Amazon",
        _ => null,
    };

    private static ItemRarity MapRarity(ItemQuality q) => q switch
    {
        ItemQuality.Inferior => ItemRarity.LowQuality,
        ItemQuality.Superior => ItemRarity.Superior,
        ItemQuality.Magic => ItemRarity.Magic,
        ItemQuality.Rare => ItemRarity.Rare,
        ItemQuality.Craft or ItemQuality.Tempered => ItemRarity.Crafted,
        ItemQuality.Set => ItemRarity.Set,
        ItemQuality.Unique => ItemRarity.Unique,
        _ => ItemRarity.Normal,
    };

    // Color reflects rarity for magic+; normal-tier items (incl. runewords) are gray when socketed
    // or ethereal, otherwise white.
    private static string ComputeColor(ItemData d)
    {
        if (d.IsRuneword) return "gray";
        return d.Rarity switch
        {
            ItemRarity.Magic => "magic",
            ItemRarity.Rare => "rare",
            ItemRarity.Set => "set",
            ItemRarity.Unique => "unique",
            ItemRarity.Crafted => "crafted",
            _ => d.Ethereal || d.SocketCount > 0 ? "gray" : "white",
        };
    }

    private static BaseTier Tier(DataFile file, DataRow row, string code)
    {
        if (GetStr(file, row, "ultracode").Trim() == code) return BaseTier.Elite;
        if (GetStr(file, row, "ubercode").Trim() == code) return BaseTier.Exceptional;
        return BaseTier.Normal;
    }

    // ---- required level (computed; not stored in the save) ----

    private int ComputeReqLevel(Item item, DataFile file, DataRow row)
    {
        int req = GetInt(file, row, "levelreq");
        if (item.Quality == ItemQuality.Unique && _uniqueLvlReq.TryGetValue((int)item.FileIndex, out int u))
            req = Math.Max(req, u);
        if (item.Quality == ItemQuality.Set && _setLvlReq.TryGetValue((int)item.FileIndex, out int s))
            req = Math.Max(req, s);
        if (item.Quality is ItemQuality.Magic or ItemQuality.Rare or ItemQuality.Craft)
        {
            foreach (var id in item.MagicPrefixIds) req = Math.Max(req, AffixLvlReq(_magicPrefixLvlReq, id));
            foreach (var id in item.MagicSuffixIds) req = Math.Max(req, AffixLvlReq(_magicSuffixLvlReq, id));
        }
        if (item.IsRuneword)
            foreach (var socket in item.SocketedItems) req = Math.Max(req, MiscLevelReq(socket.Code));
        foreach (var list in item.StatLists)
            foreach (var stat in list.Stats)
                if (stat.Stat == "item_levelreq") req = Math.Max(req, stat.Value);
        return req;
    }

    private static int AffixLvlReq(List<int> table, int id) => id >= 1 && id <= table.Count ? table[id - 1] : 0;

    private static int MiscLevelReq(string code)
    {
        var (file, row) = BaseRow(code);
        return file is not null && row is not null ? GetInt(file, row, "levelreq") : 0;
    }

    // ---- data-table access ----

    private static (DataFile? file, DataRow? row) BaseRow(string code)
    {
        var items = Core.MetaData.ItemsData;
        string c = code.Trim();
        if (items.IsArmor(c)) return (items.ArmorData, items.ArmorData.GetByColumnAndValue("code", c));
        if (items.IsWeapon(c)) return (items.WeaponsData, items.WeaponsData.GetByColumnAndValue("code", c));
        if (items.IsMisc(c)) return (items.MiscData, items.MiscData.GetByColumnAndValue("code", c));
        return (null, null);
    }

    private static int GetInt(DataFile file, DataRow row, string col) =>
        file.ColumnNames.ContainsKey(col) ? row[col].ToInt32() : 0;

    private static string GetStr(DataFile file, DataRow row, string col) =>
        file.ColumnNames.ContainsKey(col) ? row[col].Value : "";

    // ---- TSV loaders (header-indexed; "*" prefix stripped to match DataFile naming) ----

    private static Dictionary<int, int> LoadKeyValMap(string file, string keyCol, string valCol)
    {
        var map = new Dictionary<int, int>();
        var lines = File.ReadAllLines(file);
        if (lines.Length == 0) return map;
        var header = lines[0].Split('\t');
        int ki = Array.FindIndex(header, h => h.TrimStart('*').Trim() == keyCol);
        int vi = Array.FindIndex(header, h => h.TrimStart('*').Trim() == valCol);
        if (ki < 0 || vi < 0) return map;
        foreach (var line in lines.Skip(1))
        {
            var c = line.Split('\t');
            if (ki < c.Length && vi < c.Length && int.TryParse(c[ki], out int k) && int.TryParse(c[vi], out int v))
                map.TryAdd(k, v);
        }
        return map;
    }

    // skills.txt: "*Id" (skill id) -> "skill" (name column).
    private static Dictionary<int, string> LoadSkillNames(string file)
    {
        var map = new Dictionary<int, string>();
        if (!File.Exists(file)) return map;
        var lines = File.ReadAllLines(file);
        if (lines.Length == 0) return map;
        var header = lines[0].Split('\t');
        int idi = Array.FindIndex(header, h => h.TrimStart('*').Trim() == "Id");
        int ni = Array.FindIndex(header, h => h.Trim() == "skill");
        if (idi < 0 || ni < 0) return map;
        foreach (var line in lines.Skip(1))
        {
            var c = line.Split('\t');
            if (idi < c.Length && ni < c.Length && int.TryParse(c[idi], out int id))
                map.TryAdd(id, c[ni].Trim());
        }
        return map;
    }

    private static List<int> LoadColumnInts(string file, string col)
    {
        var list = new List<int>();
        var lines = File.ReadAllLines(file);
        if (lines.Length == 0) return list;
        var header = lines[0].Split('\t');
        int ci = Array.FindIndex(header, h => h.TrimStart('*').Trim() == col);
        if (ci < 0) return list;
        foreach (var line in lines.Skip(1))
        {
            var c = line.Split('\t');
            list.Add(ci < c.Length && int.TryParse(c[ci], out int v) ? v : 0);
        }
        return list;
    }
}
