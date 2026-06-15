using D2SLib;
using D2SLib.IO;
using D2SLib.Model.Save;

namespace D2RItemInspector;

/// <summary>
/// Reads a .d2i shared-stash file into a <see cref="StashData"/> by walking its tabs. Each tab is a
/// 64-byte header (magic 0xAA55AA55, game version, gold, byte size) followed by a "JM" item list.
/// </summary>
public sealed class SharedStashInspector
{
    private const uint TabMagic = 0xAA55AA55;
    private const byte JmJ = 0x4A; // 'J'
    private const byte JmM = 0x4D; // 'M'

    private readonly ItemMapping _items;

    public SharedStashInspector(ItemMapping items) => _items = items;

    public StashData Collect(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var data = new StashData { FileName = Path.GetFileName(path), ByteLength = bytes.Length };
        try
        {
            using var reader = new BitReader(bytes);
            int offset = 0, tabIndex = 0;
            while (offset + 64 <= bytes.Length && BitConverter.ToUInt32(bytes, offset) == TabMagic)
            {
                ReadTab(reader, bytes, ref offset, tabIndex++, data);
            }
            data.TabCount = tabIndex;
            data.Parsed = true;
        }
        catch (Exception ex)
        {
            data.Parsed = false;
            data.Error = $"[FAILED: {ex.GetType().Name}] {ex.Message}";
        }
        return data;
    }

    private void ReadTab(BitReader reader, byte[] bytes, ref int offset, int tabIndex, StashData data)
    {
        uint gameVersion = BitConverter.ToUInt32(bytes, offset + 8);
        uint gold = BitConverter.ToUInt32(bytes, offset + 12);
        int size = (int)BitConverter.ToUInt32(bytes, offset + 16);

        // A trailing footer block has no "JM" item-list marker at +64; skip over it (no tab emitted).
        if (!(bytes[offset + 64] == JmJ && bytes[offset + 65] == JmM))
        {
            offset += size;
            return;
        }

        // Items in this tab are encoded for this game version; select the matching data tables.
        Core.UseMetaDataForVersion(gameVersion);
        reader.SeekBits((offset + 64) * 8);
        reader.ReadUInt16(); // "JM" marker
        int count = reader.ReadUInt16();
        var tab = new StashTabData { Index = tabIndex, Gold = gold, DeclaredCount = count };

        for (int i = 0; i < count; i++)
        {
            int itemStartBit = reader.Position;
            try
            {
                var item = Item.Read(reader, gameVersion);
                if (_items.Map(item) is { } mapped) tab.Items.Add(mapped);
            }
            catch (Exception ex)
            {
                tab.Error = $"[item #{i} @bit {itemStartBit} (byte {itemStartBit / 8}) FAILED: {ex.Message}]";
                break;
            }
        }

        data.Tabs.Add(tab);
        offset += size;
    }
}
