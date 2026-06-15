using D2SLib.IO;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace D2SLib.Model.Save;

public sealed class D2S : IDisposable
{
    private D2S(IBitReader reader)
    {
        Header = Header.Read(reader);
        // Pre-RotW (v<0x69) and RotW (v0x69+) saves encode items with different data tables; make
        // the matching set active before any item/stat is read below.
        Core.UseMetaDataForVersion(Header.Version);
        ActiveWeapon = reader.ReadUInt32();
        if (Header.Version >= 0x69)
        {
            // RotW (v0x69+): old Name at 0x14 removed; Status/ClassId shifted to 0x14/0x18.
            // The assigned-skills array still begins at 0x28 (unchanged). The 16 new RotW bytes
            // are NOT here — they sit just AFTER the mercenary struct (see below).
            Status = Status.Read(reader.ReadByte());    // 0x14
            Progression = reader.ReadByte();             // 0x15
            Unk0x0026 = reader.ReadBytes(2);             // 0x16-0x17
            ClassId = reader.ReadByte();                 // 0x18
            Unk0x0029 = reader.ReadBytes(2);             // 0x19-0x1A
            Level = reader.ReadByte();                   // 0x1B
            Created = reader.ReadUInt32();               // 0x1C-0x1F
            LastPlayed = reader.ReadUInt32();            // 0x20-0x23
            Unk0x0034 = reader.ReadBytes(4);             // 0x24-0x27
        }
        else
        {
            Name = reader.ReadString(16);                // 0x14-0x23: old name position
            Status = Status.Read(reader.ReadByte());     // 0x24
            Progression = reader.ReadByte();             // 0x25
            Unk0x0026 = reader.ReadBytes(2);             // 0x26-0x27
            ClassId = reader.ReadByte();                 // 0x28
            Unk0x0029 = reader.ReadBytes(2);             // 0x29-0x2A
            Level = reader.ReadByte();                   // 0x2B
            Created = reader.ReadUInt32();               // 0x2C-0x2F
            LastPlayed = reader.ReadUInt32();            // 0x30-0x33
            Unk0x0034 = reader.ReadBytes(4);             // 0x34-0x37
        }
        AssignedSkills = Enumerable.Range(0, 16).Select(_ => Skill.Read(reader)).ToArray();
        LeftSkill = Skill.Read(reader);
        RightSkill = Skill.Read(reader);
        LeftSwapSkill = Skill.Read(reader);
        RightSwapSkill = Skill.Read(reader);
        Appearances = Appearances.Read(reader);
        Location = Locations.Read(reader);
        MapId = reader.ReadUInt32();
        Unk0x00af = reader.ReadBytes(2);
        Mercenary = Mercenary.Read(reader);
        if (Header.Version >= 0x69)
        {
            // RotW (v0x69+): 16 new bytes (unidentified, observed all-zero) sit between the
            // mercenary struct and the realm-data block. Reading them here keeps the merc struct
            // at its correct offset; misplacing this skip earlier silently zeroed the mercenary
            // (read id 0 -> merc item list skipped) for all RotW characters. RotW also enlarges
            // the realm/name/trailing region to 108 + 16 + 88 = 212 bytes.
            reader.ReadBytes(16);
            RealmData = reader.ReadBytes(108);
            Name = reader.ReadString(16);
            Unk0x00_ = reader.ReadBytes(88);
        }
        else
        {
            // Pre-RotW (D2R v0x62 and classic): realm 76 + name 16 + trailing 52 = 144 bytes,
            // and no 16-byte post-merc block. (Matches upstream locbones/D2SLib-D2R.)
            RealmData = reader.ReadBytes(76);
            Name = reader.ReadString(16);
            Unk0x00_ = reader.ReadBytes(52);
        }
        Quests = QuestsSection.Read(reader);
        Waypoints = WaypointsSection.Read(reader);
        NPCDialog = NPCDialogSection.Read(reader);
        Attributes = Attributes.Read(reader);

        ClassSkills = ClassSkills.Read(reader, ClassId);
        PlayerItemList = ItemList.Read(reader, Header.Version);
        PlayerCorpses = CorpseList.Read(reader, Header.Version);

        // RotW (v0x69+) always writes the mercenary + golem sections, even for the new Warlock
        // class (ClassId 7), whose status byte leaves the expansion bit clear. Keying off the
        // status bit alone would skip those sections and silently drop their item data, so for
        // RotW saves we read them based on the version instead.
        if (Status.IsExpansion || Header.Version >= 0x69)
        {
            MercenaryItemList = MercenaryItemList.Read(reader, Mercenary, Header.Version);
            Golem = Golem.Read(reader, Header.Version);
        }
    }

    //0x0000
    public Header Header { get; set; }
    //0x0010
    public uint ActiveWeapon { get; set; }
    //0x0014 sizeof(16)
    public string Name { get; set; }
    //0x0024
    public Status Status { get; set; }
    //0x0025
    [JsonIgnore]
    public byte Progression { get; set; }
    //0x0026 [unk = 0x0, 0x0]
    [JsonIgnore]
    public byte[]? Unk0x0026 { get; set; }
    //0x0028
    public byte ClassId { get; set; }
    //0x0029 [unk = 0x10, 0x1E]
    [JsonIgnore]
    public byte[]? Unk0x0029 { get; set; }
    //0x002b
    public byte Level { get; set; }
    //0x002c
    public uint Created { get; set; }
    //0x0030
    public uint LastPlayed { get; set; }
    //0x0034 [unk = 0xff, 0xff, 0xff, 0xff]
    [JsonIgnore]
    public byte[]? Unk0x0034 { get; set; }
    //0x0038
    public Skill[] AssignedSkills { get; set; }
    //0x0078
    public Skill LeftSkill { get; set; }
    //0x007c
    public Skill RightSkill { get; set; }
    //0x0080
    public Skill LeftSwapSkill { get; set; }
    //0x0084
    public Skill RightSwapSkill { get; set; }
    //0x0088 [char menu appearance]
    public Appearances Appearances { get; set; }
    //0x00a8
    public Locations Location { get; set; }
    //0x00ab
    public uint MapId { get; set; }
    //0x00af [unk = 0x0, 0x0]
    [JsonIgnore]
    public byte[]? Unk0x00af { get; set; }
    //0x00b1
    public Mercenary Mercenary { get; set; }
    //0x00bf [unk = 0x0] (server related data)
    [JsonIgnore]
    public byte[]? RealmData { get; set; }
    [JsonIgnore]
    public byte[]? Unk0x00_ { get; set; }
    //0x014b
    public QuestsSection Quests { get; set; }
    //0x0279
    public WaypointsSection Waypoints { get; set; }
    //0x02c9
    public NPCDialogSection NPCDialog { get; set; }
    //0x2fc
    public Attributes Attributes { get; set; }

    public ClassSkills ClassSkills { get; set; }

    public ItemList PlayerItemList { get; set; }
    public CorpseList PlayerCorpses { get; set; }
    public MercenaryItemList? MercenaryItemList { get; set; }
    public Golem? Golem { get; set; }

    public void Write(IBitWriter writer)
    {
        Header.Write(writer);
        writer.WriteUInt32(ActiveWeapon);
        //writer.WriteString(Name, 16);
        for (int i=0;i<16;i++) writer.WriteByte(0x00);
        Status.Write(writer);
        writer.WriteByte(Progression);
        //Unk0x0026
        writer.WriteBytes(Unk0x0026 ?? new byte[2]);

        writer.WriteByte(ClassId);
        //Unk0x0029
        writer.WriteBytes(Unk0x0029 ?? stackalloc byte[] { 0x10, 0x1e });

        writer.WriteByte(Level);

        writer.WriteUInt32(Created);
        writer.WriteUInt32(LastPlayed);
        //Unk0x0034
        writer.WriteBytes(Unk0x0034 ?? stackalloc byte[] { 0xff, 0xff, 0xff, 0xff });
        
        for (int i = 0; i < 16; i++) AssignedSkills[i].Write(writer);
        
        LeftSkill.Write(writer);
        RightSkill.Write(writer);
        LeftSwapSkill.Write(writer);
        RightSwapSkill.Write(writer);
        Appearances.Write(writer);
        Location.Write(writer);
        writer.WriteUInt32(MapId);
        //0x00af [unk = 0x0, 0x0]
        //writer.WriteBytes(Unk0x00af ?? new byte[2]);
        Mercenary.Write(writer);
        //0x00bf [unk = 0x0] (server related data)
        writer.WriteBytes(RealmData ?? new byte[76]);
        writer.WriteString(Name, 16);
        for (int i = 0; i < 52; i++) writer.WriteByte(0x00);
        Quests.Write(writer);
        Waypoints.Write(writer);
        NPCDialog.Write(writer);
        Attributes.Write(writer);
        ClassSkills.Write(writer);
        PlayerItemList.Write(writer, Header.Version);
        PlayerCorpses.Write(writer, Header.Version);
        if (Status.IsExpansion)
        {
            MercenaryItemList?.Write(writer, Mercenary, Header.Version);
            Golem?.Write(writer, Header.Version);
        }
    }

    public static D2S Read(ReadOnlySpan<byte> bytes)
    {
        using var reader = new BitReader(bytes);
        var d2s = new D2S(reader);
        //Debug.Assert(reader.Position == (bytes.Length * 8));
        return d2s;
    }

    public static MemoryOwner<byte> WritePooled(D2S d2s)
    {
        using var writer = new BitWriter();
        d2s.Write(writer);
        var bytes = writer.ToPooledArray();
        Header.Fix(bytes.Span);
        return bytes;
    }

    public static byte[] Write(D2S d2s)
    {
        using var writer = new BitWriter();
        d2s.Write(writer);
        byte[] bytes = writer.ToArray();
        Header.Fix(bytes);
        return bytes;
    }

    public void Dispose()
    {
        Waypoints.Dispose();
        Status.Dispose();
        Quests.Dispose();
        PlayerItemList.Dispose();
        PlayerCorpses.Dispose();
        MercenaryItemList?.Dispose();
    }
}
