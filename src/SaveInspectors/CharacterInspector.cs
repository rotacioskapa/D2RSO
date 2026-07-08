using D2SLib;
using D2SLib.Model.Save;

using D2RItemInspector.Model;
using D2RItemInspector.ItemEnrichment;

namespace D2RItemInspector.SaveInspectors;

/// <summary>Reads a .d2s character save into a <see cref="CharacterData"/> (inventory, corpse, merc, golem).</summary>
public sealed class CharacterInspector
{
    // ClassId index -> name; index 7 (Warlock) is the new RotW expansion class.
    private static readonly string[] ClassNames =
        { "Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin", "Warlock" };

    private readonly ItemMapping _items;

    public CharacterInspector(ItemMapping items) => _items = items;

    public CharacterData Collect(string path)
    {
        var data = new CharacterData { FileName = Path.GetFileName(path) };
        try
        {
            var character = Core.ReadD2S(path);
            data.Name = character.Name;
            data.Class = ClassName(character.ClassId);
            data.Level = character.Level;
            data.Version = character.Header.Version;
            data.IsExpansion = character.Status.IsExpansion;

            data.InventoryCount = character.PlayerItemList.Count;
            data.Inventory.AddRange(_items.MapAll(character.PlayerItemList.Items));

            if (character.PlayerCorpses is { Count: > 0 } corpses)
            {
                data.CorpseCount = corpses.Count;
                foreach (var corpse in corpses.Corpses)
                    data.Corpse.AddRange(_items.MapAll(corpse.ItemList.Items));
            }

            if (character.MercenaryItemList is { } merc)
            {
                data.Mercenary = new MercenaryData
                {
                    Id = character.Mercenary.Id,
                    DeclaredCount = merc.ItemList?.Count ?? 0,
                    Items = merc.ItemList is { } mi ? _items.MapAll(mi.Items) : new List<ItemData>(),
                };
            }

            if (character.Golem is { } golem)
            {
                data.Golem = new GolemData
                {
                    Exists = golem.Exists,
                    Item = golem.Item is { } gi ? _items.Map(gi) : null,
                };
            }

            data.Parsed = true;
        }
        catch (Exception ex)
        {
            data.Parsed = false;
            data.Error = $"[FAILED: {ex.GetType().Name}] {ex.Message}";
        }
        return data;
    }

    private static string ClassName(byte classId) =>
        classId < ClassNames.Length ? ClassNames[classId] : $"Class{classId}";
}
