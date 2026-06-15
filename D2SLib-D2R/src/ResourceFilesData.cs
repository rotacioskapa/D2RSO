using D2SLib.Model.Data;
using System.Reflection;

namespace D2SLib;

public sealed class ResourceFilesData
{
    // RotW (v0x69+) changed several item/stat tables. Pre-RotW (vanilla D2R, v<0x69) saves encode
    // their items with the original tables, so we keep BOTH sets embedded and pick by save version.
    private const uint RotWVersion = 0x69;

    private ResourceFilesData()
    {
        MetaData = LoadMetaData("ItemStatCost.txt", "Armor.txt", "Weapons.txt", "Misc.txt");
        LegacyMetaData = LoadMetaData("Legacy_ItemStatCost.txt", "Legacy_Armor.txt", "Legacy_Weapons.txt", "Legacy_Misc.txt");
    }

    public static ResourceFilesData Instance { get; } = new();

    /// <summary>RotW (v0x69+) data tables.</summary>
    public MetaData MetaData { get; set; }

    /// <summary>Pre-RotW (vanilla D2R, v&lt;0x69) data tables.</summary>
    public MetaData LegacyMetaData { get; set; }

    /// <summary>Picks the table set matching a save/item version.</summary>
    public MetaData ForVersion(uint version) => version >= RotWVersion ? MetaData : LegacyMetaData;

    private static MetaData LoadMetaData(string itemStatCost, string armor, string weapons, string misc)
    {
        ItemStatCostData itemStatCostData;
        ArmorData armorData;
        WeaponsData weaponsData;
        MiscData miscData;
        using (Stream s = GetResource(itemStatCost)) { itemStatCostData = ItemStatCostData.Read(s); }
        using (Stream s = GetResource(armor)) { armorData = ArmorData.Read(s); }
        using (Stream s = GetResource(weapons)) { weaponsData = WeaponsData.Read(s); }
        using (Stream s = GetResource(misc)) { miscData = MiscData.Read(s); }
        return new MetaData(itemStatCostData, new ItemsData(armorData, weaponsData, miscData));
    }

    private static Stream GetResource(string file)
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream($"D2SLib.Resources.{file}")
            ?? throw new InvalidOperationException($"{file} was not found in embedded resources.");
    }
}
