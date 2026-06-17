namespace D2RItemInspector;

public enum ItemRarity { LowQuality, Normal, Superior, Magic, Rare, Crafted, Set, Unique }

public enum BaseTier { Normal, Exceptional, Elite }

/// <summary>A single item with the data needed for both the console output and the HTML report.</summary>
public sealed class ItemData
{
    // Used by the console renderer (full name incl. base/eth/sockets), unchanged behaviour.
    public required string Name { get; init; }
    public int Quantity { get; init; } = 1;
    public List<string> Sockets { get; init; } = new();

    // Enriched fields for the HTML report (populated by ItemEnricher).
    public string DisplayName { get; set; } = "";   // clean name for the report's Name column
    public ItemRarity Rarity { get; set; }
    public bool IsRuneword { get; set; }
    public string? RunewordName { get; set; }
    public string ColorClass { get; set; } = "white"; // CSS class: white/gray/magic/rare/set/unique/crafted
    public string? TypeCategory { get; set; }        // null = not equipment (excluded from report)
    public string BaseName { get; set; } = "";
    public BaseTier BaseQuality { get; set; }
    public string? SetName { get; set; }
    public List<string> SetBonuses { get; set; } = new(); // whole-set bonuses (for the Set-name tooltip)
    public int SocketCount { get; set; }
    public int RequiredLevel { get; set; }
    public int RequiredStrength { get; set; }
    public int RequiredDexterity { get; set; }
    public string? AllowedClass { get; set; }        // null = usable by all classes
    public bool Ethereal { get; set; }
    public bool Equipped { get; set; }
    public List<StatLine> Stats { get; set; } = new(); // formatted mod lines for the hover tooltip
}

/// <summary>One formatted mod line for the tooltip. <see cref="Set"/> = granted by a partial set bonus.</summary>
public sealed class StatLine
{
    public string Text { get; init; } = "";
    public bool Set { get; init; }
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
