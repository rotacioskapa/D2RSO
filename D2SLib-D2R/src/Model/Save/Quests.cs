using D2SLib.IO;
using System.Diagnostics;
using System.Text;

namespace D2SLib.Model.Save;

public sealed class QuestsSection : IDisposable
{
    private readonly QuestsDifficulty[] _difficulties = new QuestsDifficulty[3];

    //0x014f [quests header identifier = 0x57, 0x6f, 0x6f, 0x21 "Woo!"]
    public string Header { get; private set; } = string.Empty;
    //0x0153 [version = 0x6, 0x0, 0x0, 0x0]
    public uint? Version { get; set; }
    //0x0153 [quests header length = 0x2a, 0x1]
    public ushort? Length { get; set; }

    public QuestsDifficulty Normal => _difficulties[0];
    public QuestsDifficulty Nightmare => _difficulties[1];
    public QuestsDifficulty Hell => _difficulties[2];

    public void Write(IBitWriter writer)
    {
        //writer.WriteUInt32(Magic ?? 0x1);
        writer.WriteString(Header, 4);
        writer.WriteUInt32(Version ?? 0x06);
        writer.WriteUInt16(Length ?? 0x012A);

        for (int i = 0; i < _difficulties.Length; i++)
        {
            _difficulties[i].Write(writer);
        }
    }

    public static QuestsSection Read(IBitReader reader)
    {
        var questSection = new QuestsSection();
        //string header = reader.ReadString(4);
        questSection.Header = reader.ReadString(4);
        questSection.Version = reader.ReadUInt32();
        questSection.Length = reader.ReadUInt16();
        //{ Seems to be reading older versions?
        //    Magic = reader.ReadUInt32(),
        //    Header = reader.ReadUInt32(),
        //    Version = reader.ReadUInt32(),
        //    Length = reader.ReadUInt16()
        //};

        for (int i = 0; i < questSection._difficulties.Length; i++)
        {
            questSection._difficulties[i] = QuestsDifficulty.Read(reader);
        }

        return questSection;
    }

    [Obsolete("Try the direct-read overload!")]
    public static QuestsSection Read(byte[] bytes)
    {
        using var reader = new BitReader(bytes);
        return Read(reader);
    }

    [Obsolete("Try the non-allocating overload!")]
    public static byte[] Write(QuestsSection questSection)
    {
        using var writer = new BitWriter();
        questSection.Write(writer);
        return writer.ToArray();
    }

    public void Dispose()
    {
        for (int i = 0; i < _difficulties.Length; i++)
        {
            Interlocked.Exchange(ref _difficulties[i]!, null)?.Dispose();
        }
    }
}

public sealed class QuestsDifficulty : IDisposable
{
    private QuestsDifficulty(IBitReader reader)
    {
        int pos = reader.Position / 8;
        ActI = ActIQuests.Read(reader);
        ActII = ActIIQuests.Read(reader);
        ActIII = ActIIIQuests.Read(reader);
        ActIV = ActIVQuests.Read(reader);
        ActV = ActVQuests.Read(reader);
    }

    public ActIQuests ActI { get; set; }
    public ActIIQuests ActII { get; set; }
    public ActIIIQuests ActIII { get; set; }
    public ActIVQuests ActIV { get; set; }
    public ActVQuests ActV { get; set; }

    public void Write(IBitWriter writer)
    {
        ActI.Write(writer);
        ActII.Write(writer);
        ActIII.Write(writer);
        ActIV.Write(writer);
        ActV.Write(writer);
    }

    public static QuestsDifficulty Read(IBitReader reader)
    {
        var qd = new QuestsDifficulty(reader);
        return qd;
    }

    [Obsolete("Try the direct-read overload!")]
    public static QuestsDifficulty Read(ReadOnlySpan<byte> bytes)
    {
        using var reader = new BitReader(bytes);
        return Read(reader);
    }

    [Obsolete("Try the non-allocating overload!")]
    public static byte[] Write(QuestsDifficulty questsDifficulty)
    {
        using var writer = new BitWriter();
        questsDifficulty.Write(writer);
        Debug.Assert(writer.Position == 96 * 8);
        return writer.ToArray();
    }

    public void Dispose()
    {
        ActI.Dispose();
        ActII.Dispose();
        ActIII.Dispose();
        ActIV.Dispose();
        ActV.Dispose();
    }
}


public sealed class Quest : IDisposable
{
    private InternalBitArray _flags;

    private Quest(InternalBitArray flags) => _flags = flags;

    public bool RewardGranted { get => _flags[0]; set => _flags[0] = value; }
    public bool RewardPending { get => _flags[1]; set => _flags[1] = value; }
    public bool Started { get => _flags[2]; set => _flags[2] = value; }
    public bool LeftTown { get => _flags[3]; set => _flags[3] = value; }
    public bool EnterArea { get => _flags[4]; set => _flags[4] = value; }
    public bool Custom1 { get => _flags[5]; set => _flags[5] = value; }
    public bool DrankPotionOfLife { get => _flags[6]; set => _flags[6] = value; }
    public bool Custom2 { get => _flags[7]; set => _flags[7] = value; }
    public bool ReadScrollofResistance { get => _flags[8]; set => _flags[8] = value; }
    public bool Custom3 { get => _flags[9]; set => _flags[9] = value; }
    public bool Custom4 { get => _flags[10]; set => _flags[10] = value; }
    public bool SecretCowLevelCompleted { get => _flags[11]; set => _flags[11] = value; }
    public bool QuestLog { get => _flags[12]; set => _flags[12] = value; }
    public bool PrimaryGoalAchieved { get => _flags[13]; set => _flags[13] = value; }
    public bool CompletedNow { get => _flags[14]; set => _flags[14] = value; }
    public bool CompletedBefore { get => _flags[15]; set => _flags[15] = value; }

    public void Write(IBitWriter writer)
    {
        ushort flags = 0x0;
        ushort i = 1;
        foreach (var flag in _flags)
        {
            if (flag)
            {
                flags |= i;
            }
            i <<= 1;
        }
        writer.WriteUInt16(flags);
    }

    public static Quest Read(IBitReader reader)
    {
        Span<byte> bytes = stackalloc byte[2];
        reader.ReadBytes(bytes);
        var bits = new InternalBitArray(bytes);
        return new Quest(bits);
    }

    [Obsolete("Try the direct-read overload!")]
    public static Quest Read(ReadOnlySpan<byte> bytes)
    {
        var bits = new InternalBitArray(bytes);
        return new Quest(bits);
    }

    [Obsolete("Try the non-allocating overload!")]
    public static byte[] Write(Quest quest)
    {
        using var writer = new BitWriter();
        quest.Write(writer);
        return writer.ToArray();
    }

    public void Dispose() => Interlocked.Exchange(ref _flags!, null)?.Dispose();
}

public sealed class ActIQuests : IDisposable
{
    private readonly Quest[] _quests = new Quest[6];

    public bool TalkedToWarriv { get; private set; } = false;
    public Quest DenOfEvil => _quests[0];
    public Quest SistersBurialGrounds => _quests[1];
    public Quest ToolsOfTheTrade => _quests[2];
    public Quest TheSearchForCain => _quests[3];
    public Quest TheForgottenTower => _quests[4];
    public Quest SistersToTheSlaughter => _quests[5];

    public void Write(IBitWriter writer)
    {
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TalkedToWarriv)[0], 0x00 });
        for (int i = 0; i < _quests.Length; i++)
        {
            _quests[i].Write(writer);
        }
    }

    public static ActIQuests Read(IBitReader reader)
    {
        int pos = reader.Position / 8;
        var quests = new ActIQuests();
        quests.TalkedToWarriv = (reader.ReadBytes(2)[0] == 0x01);
        for (int i = 0; i < quests._quests.Length; i++)
        {
            quests._quests[i] = Quest.Read(reader);
        }
        return quests;
    }

    public void Dispose()
    {
        for (int i = 0; i < _quests.Length; i++)
        {
            Interlocked.Exchange(ref _quests[i]!, null)?.Dispose();
        }
    }
}

public sealed class ActIIQuests : IDisposable
{
    private readonly Quest[] _quests = new Quest[6];

    public bool TraveledToAct { get; private set; } = false;
    public bool TalkedToJerhyn { get; private set; } = false;
    public Quest RadamentsLair => _quests[0];
    public Quest TheHoradricStaff => _quests[1];
    public Quest TaintedSun => _quests[2];
    public Quest ArcaneSanctuary => _quests[3];
    public Quest TheSummoner => _quests[4];
    public Quest TheSevenTombs => _quests[5];

    public void Write(IBitWriter writer)
    {
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TraveledToAct)[0], 0x00 });
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TalkedToJerhyn)[0], 0x00 });
        for (int i = 0; i < _quests.Length; i++)
        {
            _quests[i].Write(writer);
        }
    }

    public static ActIIQuests Read(IBitReader reader)
    {
        var quests = new ActIIQuests();
        quests.TraveledToAct = (reader.ReadBytes(2)[0] == 0x01);
        quests.TalkedToJerhyn = (reader.ReadBytes(2)[0] == 0x01);
        for (int i = 0; i < quests._quests.Length; i++)
        {
            quests._quests[i] = Quest.Read(reader);
        }
        return quests;
    }

    public void Dispose()
    {
        for (int i = 0; i < _quests.Length; i++)
        {
            Interlocked.Exchange(ref _quests[i]!, null)?.Dispose();
        }
    }
}

public sealed class ActIIIQuests : IDisposable
{
    private readonly Quest[] _quests = new Quest[6];

    public bool TraveledToAct { get; private set; } = false;
    public bool TalkedToHratli { get; private set; } = false;
    public Quest LamEsensTome => _quests[0];
    public Quest KhalimsWill => _quests[1];
    public Quest BladeOfTheOldReligion => _quests[2];
    public Quest TheGoldenBird => _quests[3];
    public Quest TheBlackenedTemple => _quests[4];
    public Quest TheGuardian => _quests[5];

    public void Write(IBitWriter writer)
    {
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TraveledToAct)[0], 0x00 });
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TalkedToHratli)[0], 0x00 });
        for (int i = 0; i < _quests.Length; i++)
        {
            _quests[i].Write(writer);
        }
    }

    public static ActIIIQuests Read(IBitReader reader)
    {
        var quests = new ActIIIQuests();
        quests.TraveledToAct = (reader.ReadBytes(2)[0] == 0x01);
        quests.TalkedToHratli = (reader.ReadBytes(2)[0] == 0x01);
        for (int i = 0; i < quests._quests.Length; i++)
        {
            quests._quests[i] = Quest.Read(reader);
        }
        return quests;
    }

    public void Dispose()
    {
        for (int i = 0; i < _quests.Length; i++)
        {
            Interlocked.Exchange(ref _quests[i]!, null)?.Dispose();
        }
    }
}

public sealed class ActIVQuests : IDisposable
{
    private readonly Quest[] _quests = new Quest[3];

    public bool TraveledToAct { get; private set; } = false;
    public bool TalkedToTyreal { get; private set; } = false;
    public Quest TheFallenAngel => _quests[0];
    public Quest TerrorsEnd => _quests[1];
    public Quest Hellforge => _quests[2];

    public void Write(IBitWriter writer)
    {
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TraveledToAct)[0], 0x00 });
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TalkedToTyreal)[0], 0x00 });
        for (int i = 0; i < _quests.Length; i++)
        {
            _quests[i].Write(writer);
        }
        for(int i=0;i<6;i++) writer.WriteByte(0x00);
    }

    public static ActIVQuests Read(IBitReader reader)
    {
        var quests = new ActIVQuests();
        quests.TraveledToAct = (reader.ReadBytes(2)[0] == 0x01);
        quests.TalkedToTyreal = (reader.ReadBytes(2)[0] == 0x01);
        for (int i = 0; i < quests._quests.Length; i++)
        {
            quests._quests[i] = Quest.Read(reader);
        }
        reader.ReadBytes(6);
        return quests;
    }

    public void Dispose()
    {
        for (int i = 0; i < _quests.Length; i++)
        {
            Interlocked.Exchange(ref _quests[i]!, null)?.Dispose();
        }
    }
}

public sealed class ActVQuests : IDisposable
{
    private readonly Quest[] _quests = new Quest[6];


    public bool TraveledToAct { get; private set; } = false;
    public bool TalkedToCain { get; private set; } = false;
    public Quest SiegeOnHarrogath => _quests[0];
    public Quest RescueOnMountArreat => _quests[1];
    public Quest PrisonOfIce => _quests[2];
    public Quest BetrayalOfHarrogath => _quests[3];
    public Quest RiteOfPassage => _quests[4];
    public Quest EveOfDestruction => _quests[5];
    public bool ResetStats { get; private set; } = false;
    public bool CompletedDifficulty { get; set; } = false;

    public void Write(IBitWriter writer)
    {
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TraveledToAct)[0], 0x00 });
        writer.WriteBytes(new byte[2] { BitConverter.GetBytes(TalkedToCain)[0], 0x00 });
        writer.WriteBytes(new byte[4] { 0x00, 0x00, 0x00, 0x00 });
        for (int i = 0; i < _quests.Length; i++)
        {
            _quests[i].Write(writer);
        }
        writer.WriteByte(BitConverter.GetBytes(ResetStats)[0]);
        writer.WriteByte((byte)(CompletedDifficulty ? 0x80 : 0x00));
        for(int i=0;i<12;i++) writer.WriteByte(0x00);
    }

    public static ActVQuests Read(IBitReader reader)
    {
        var quests = new ActVQuests();
        quests.TraveledToAct = (reader.ReadBytes(2)[0]) == 0x01;
        quests.TalkedToCain = (reader.ReadBytes(2)[0]) == 0x01;
        reader.ReadBytes(4);    // Junk bytes padding it seems
        for (int i = 0; i < quests._quests.Length; i++)
        {
            quests._quests[i] = Quest.Read(reader);
        }
        quests.ResetStats = (reader.ReadByte() == 0x01);
        quests.CompletedDifficulty = (reader.ReadByte() == 0x80);
        reader.ReadBytes(12);    // junk bytes
        return quests;
    }

    public void Dispose()
    {
        for (int i = 0; i < _quests.Length; i++)
        {
            Interlocked.Exchange(ref _quests[i]!, null)?.Dispose();
        }
    }
}
