using D2SLib.Model.Save;

namespace D2RItemInspector;

/// <summary>Maps parsed <see cref="Item"/>s to <see cref="ItemData"/>, resolving names and stack quantities.</summary>
public sealed class ItemMapping
{
    private readonly ItemNameResolver _names;

    public ItemMapping(ItemNameResolver names) => _names = names;

    /// <summary>
    /// Builds an <see cref="ItemData"/> for one item, or returns null for an empty "ghost" shared-stash
    /// stack (CompactExtra == 1).
    /// </summary>
    public ItemData? Map(Item item)
    {
        // Stack quantity: bit0 of CompactExtra is a flag, bits1-8 the count (real count = value >> 1).
        // CompactExtra == 1 is an empty leftover/ghost stack -> skip. CompactExtra == 0 is ordinary
        // (non-stash-stackable) gear -> quantity 1.
        if (item.CompactExtra == 1) return null;
        int quantity = item.CompactExtra < 2 ? 1 : item.CompactExtra >> 1;
        return new ItemData
        {
            Name = _names.FullName(item),
            Quantity = quantity,
            Sockets = item.SocketedItems.Select(_names.FullName).ToList(),
        };
    }

    /// <summary>Maps a list of items, dropping ghost stacks.</summary>
    public List<ItemData> MapAll(IEnumerable<Item> items)
    {
        var list = new List<ItemData>();
        foreach (var item in items)
            if (Map(item) is { } data) list.Add(data);
        return list;
    }
}
