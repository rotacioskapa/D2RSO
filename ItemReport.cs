namespace D2RItemInspector;

/// <summary>One equipment item flattened for the report, with the owner/placement context that a
/// bare <see cref="ItemData"/> doesn't carry.</summary>
public sealed class ReportRow
{
    public string Name { get; init; } = "";        // display name (color column)
    public string Color { get; init; } = "white";  // CSS color class
    public string Rarity { get; init; } = "";       // for the rarity filter
    public bool Runeword { get; init; }
    public string Type { get; init; } = "";
    public string Base { get; init; } = "";
    public string BaseQuality { get; init; } = "";
    public string? Set { get; init; }
    public int Sockets { get; init; }
    public List<string> SocketItems { get; init; } = new();
    public int ReqLevel { get; init; }
    public int ReqStr { get; init; }
    public int ReqDex { get; init; }
    public string? Class { get; init; }             // allowed class (null = usable by all)
    public bool Eth { get; init; }
    public string Owner { get; init; } = "";
    public string Source { get; init; } = "";
    public List<StatLine> Stats { get; init; } = new(); // formatted mod lines for the hover tooltip
}

/// <summary>Flattens an <see cref="InspectionResult"/> into report rows, keeping only equipment.</summary>
public static class ItemReport
{
    public static List<ReportRow> Build(InspectionResult result)
    {
        var rows = new List<ReportRow>();
        foreach (var c in result.Characters.Values)
        {
            string owner = string.IsNullOrEmpty(c.Name) ? c.FileName : c.Name;
            foreach (var it in c.Inventory) Add(rows, it, owner, it.Equipped ? "Equipped" : "Stored");
            foreach (var it in c.Corpse) Add(rows, it, owner, "Corpse");
            if (c.Mercenary is { } merc) foreach (var it in merc.Items) Add(rows, it, owner, "Mercenary");
            if (c.Golem?.Item is { } golemItem) Add(rows, golemItem, owner, "Golem");
        }
        foreach (var s in result.Stashes.Values)
            foreach (var tab in s.Tabs)
                foreach (var it in tab.Items) Add(rows, it, "Shared Stash", $"Tab {tab.Index + 1}");
        return rows;
    }

    private static void Add(List<ReportRow> rows, ItemData it, string owner, string source)
    {
        if (it.TypeCategory is null) return; // not equipment -> excluded
        rows.Add(new ReportRow
        {
            Name = it.DisplayName,
            Color = it.ColorClass,
            Rarity = GroupRarity(it.Rarity),
            Runeword = it.IsRuneword,
            Type = it.TypeCategory,
            Base = it.BaseName,
            BaseQuality = it.BaseQuality.ToString(),
            Set = it.SetName,
            Sockets = it.SocketCount,
            SocketItems = it.Sockets,
            ReqLevel = it.RequiredLevel,
            ReqStr = it.RequiredStrength,
            ReqDex = it.RequiredDexterity,
            Class = it.AllowedClass,
            Eth = it.Ethereal,
            Owner = owner,
            Source = source,
            Stats = it.Stats,
        });
    }

    // Low-quality, normal and superior are all the "common" tier — one rarity group for filtering.
    private static string GroupRarity(ItemRarity r) => r switch
    {
        ItemRarity.LowQuality or ItemRarity.Normal or ItemRarity.Superior => "Normal",
        _ => r.ToString(),
    };
}
