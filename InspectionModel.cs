namespace D2RItemInspector;

/// <summary>A single item: its resolved display name, stack quantity, and any socketed item names.</summary>
public sealed class ItemData
{
    public required string Name { get; init; }
    public int Quantity { get; init; } = 1;
    public List<string> Sockets { get; init; } = new();
}

/// <summary>A character's hired mercenary and its equipped items.</summary>
public sealed class MercenaryData
{
    public uint Id { get; init; }
    public int DeclaredCount { get; init; }
    public List<ItemData> Items { get; init; } = new();
}

/// <summary>A character's iron golem (necromancer), if any.</summary>
public sealed class GolemData
{
    public bool Exists { get; init; }
    public ItemData? Item { get; init; }
}

/// <summary>Everything read from one .d2s character save.</summary>
public sealed class CharacterData
{
    public required string FileName { get; init; }
    public bool Parsed { get; set; }
    public string? Error { get; set; }

    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Level { get; set; }
    public uint Version { get; set; }
    public bool IsExpansion { get; set; }

    public int InventoryCount { get; set; }
    public List<ItemData> Inventory { get; } = new();

    public int CorpseCount { get; set; }
    public List<ItemData> Corpse { get; } = new();

    public MercenaryData? Mercenary { get; set; }
    public GolemData? Golem { get; set; }
}

/// <summary>One tab of a shared stash.</summary>
public sealed class StashTabData
{
    public int Index { get; init; }
    public uint Gold { get; init; }
    public int DeclaredCount { get; init; }
    public List<ItemData> Items { get; } = new();
    /// <summary>Set when an item in this tab failed to parse (parsing of the tab stops there).</summary>
    public string? Error { get; set; }
}

/// <summary>Everything read from one .d2i shared-stash file.</summary>
public sealed class StashData
{
    public required string FileName { get; init; }
    public long ByteLength { get; init; }
    public bool Parsed { get; set; }
    public string? Error { get; set; }
    /// <summary>Total tab blocks walked (includes trailing non-item footer blocks).</summary>
    public int TabCount { get; set; }
    public List<StashTabData> Tabs { get; } = new();
}

/// <summary>
/// The full result of inspecting a save directory: characters and shared stashes, each keyed by file name.
/// This is the structured form other apps consume; the console output is just a rendering of it.
/// </summary>
public sealed class InspectionResult
{
    public Dictionary<string, CharacterData> Characters { get; } = new();
    public Dictionary<string, StashData> Stashes { get; } = new();
}
