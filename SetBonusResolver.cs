namespace D2RItemInspector;

/// <summary>
/// Loads the whole-set bonuses from sets.txt: partial bonuses (PCode, granted at N equipped set
/// items) and the full-set bonus (FCode). Bonuses are stored as property codes (att, res-all, …)
/// rather than the expanded itemstatcost stats, so they have their own formatter here.
/// </summary>
public sealed class SetBonusResolver
{
    private readonly Dictionary<string, List<string>> _bonuses = new(StringComparer.Ordinal);

    public SetBonusResolver(string setsFile, Func<int, string?> skillName)
    {
        if (!File.Exists(setsFile)) return;
        var lines = File.ReadAllLines(setsFile);
        if (lines.Length == 0) return;
        var col = new Dictionary<string, int>();
        var header = lines[0].Split('\t');
        for (int i = 0; i < header.Length; i++) col[header[i].Trim()] = i;
        int idx = col.GetValueOrDefault("index", -1);
        if (idx < 0) return;

        foreach (var line in lines.Skip(1))
        {
            var c = line.Split('\t');
            if (idx >= c.Length || string.IsNullOrWhiteSpace(c[idx])) continue;
            var list = new List<string>();
            // Partial bonuses: tiers 2-5 set items, two properties (a/b) each.
            foreach (int tier in new[] { 2, 3, 4, 5 })
                foreach (string s in new[] { "a", "b" })
                    if (Prop(c, col, $"PCode{tier}{s}", $"PParam{tier}{s}", $"PMin{tier}{s}", $"PMax{tier}{s}", skillName) is { } p)
                        list.Add($"{p} ({tier} Items)");
            // Full-set bonus.
            for (int i = 1; i <= 8; i++)
                if (Prop(c, col, $"FCode{i}", $"FParam{i}", $"FMin{i}", $"FMax{i}", skillName) is { } p)
                    list.Add($"{p} (Full Set)");
            if (list.Count > 0) _bonuses[c[idx]] = list;
        }
    }

    public List<string>? Get(string setKey) => _bonuses.GetValueOrDefault(setKey);

    private static string? Prop(string[] c, Dictionary<string, int> col, string codeCol, string paramCol,
        string minCol, string maxCol, Func<int, string?> skillName)
    {
        int ci = col.GetValueOrDefault(codeCol, -1);
        if (ci < 0 || ci >= c.Length || string.IsNullOrWhiteSpace(c[ci])) return null;
        return Format(c[ci].Trim(), Int(c, col, paramCol), Int(c, col, minCol), Int(c, col, maxCol), skillName);
    }

    private static int Int(string[] c, Dictionary<string, int> col, string name)
    {
        int i = col.GetValueOrDefault(name, -1);
        return i >= 0 && i < c.Length && int.TryParse(c[i], out int v) ? v : 0;
    }

    // Property code -> readable text. Unknown / state-based codes return null (skipped).
    private static string? Format(string code, int param, int min, int max, Func<int, string?> skillName)
    {
        string Rng() => min == max ? min.ToString() : $"{min}-{max}";
        string Sk(int id) => skillName(id) ?? $"Skill #{id}";
        return code switch
        {
            "ac" => $"+{min} Defense",
            "ac%" => $"+{min}% Enhanced Defense",
            "ac-miss" => $"+{min} Defense vs. Missile",
            "res-all" => $"All Resistances +{min}",
            "res-fire" => $"Fire Resist +{min}%",
            "res-cold" => $"Cold Resist +{min}%",
            "res-ltng" => $"Lightning Resist +{min}%",
            "res-pois" => $"Poison Resist +{min}%",
            "res-fire-max" => $"+{min}% to Maximum Fire Resist",
            "res-cold-max" => $"+{min}% to Maximum Cold Resist",
            "res-ltng-max" => $"+{min}% to Maximum Lightning Resist",
            "res-pois-max" => $"+{min}% to Maximum Poison Resist",
            "res-pois-len" => $"Poison Length Reduced by {min}%",
            "lifesteal" => $"{min}% Life Stolen per Hit",
            "manasteal" => $"{min}% Mana Stolen per Hit",
            "mana" => $"+{min} to Mana",
            "mana%" => $"Increase Maximum Mana {min}%",
            "hp" => $"+{min} to Life",
            "hp%" => $"Increase Maximum Life {min}%",
            "str" => $"+{min} to Strength",
            "dex" => $"+{min} to Dexterity",
            "enr" => $"+{min} to Energy",
            "stam" => $"+{min} to Stamina",
            "att" => $"+{min} to Attack Rating",
            "att%" => $"+{min}% Bonus to Attack Rating",
            "allskills" => $"+{min} to All Skills",
            "mag%" => $"+{min}% Better Chance of Getting Magic Items",
            "gold%" => $"+{min}% Extra Gold from Monsters",
            "regen" => $"Replenish Life +{min}",
            "regen-mana" => $"Regenerate Mana {min}%",
            "cast1" or "cast2" or "cast3" => $"+{min}% Faster Cast Rate",
            "balance1" or "balance2" or "balance3" => $"+{min}% Faster Hit Recovery",
            "swing1" or "swing2" or "swing3" => $"+{min}% Increased Attack Speed",
            "move1" or "move2" or "move3" => $"+{min}% Faster Run/Walk",
            "block" or "block1" or "block2" or "block3" => $"+{min}% Increased Chance to Block",
            "dmg%" => $"+{min}% Enhanced Damage",
            "dmg-min" => $"+{min} to Minimum Damage",
            "dmg-max" => $"+{min} to Maximum Damage",
            "dmg-fire" => $"Adds {Rng()} Fire Damage",
            "dmg-cold" => $"Adds {Rng()} Cold Damage",
            "dmg-ltng" => $"Adds {Rng()} Lightning Damage",
            "dmg-pois" => $"Adds {Rng()} Poison Damage",
            "dmg-mag" => $"Adds {Rng()} Magic Damage",
            "dmg-demon" => $"+{min}% Damage to Demons",
            "dmg-undead" => $"+{min}% Damage to Undead",
            "fire-max" => $"+{min} to Maximum Fire Damage",
            "red-mag" => $"Magic Damage Reduced by {min}",
            "red-dmg" => $"Damage Reduced by {min}",
            "red-dmg%" => $"Damage Reduced by {min}%",
            "thorns" => $"Attacker Takes Damage of {min}",
            "pierce" => $"{min}% Piercing Attack",
            "crush" => $"{min}% Chance of Crushing Blow",
            "deadly" => $"{min}% Deadly Strike",
            "openwounds" => $"{min}% Chance of Open Wounds",
            "nofreeze" => "Cannot Be Frozen",
            "half-freeze" => "Half Freeze Duration",
            "freeze" => $"Freezes Target +{min}",
            "slow" => $"Slows Target by {min}%",
            "knock" => "Knockback",
            "light" => $"+{min} to Light Radius",
            "oskill" => $"+{min} to {Sk(param)}",
            "aura" => $"Level {min} {Sk(param)} Aura When Equipped",
            "fireskill" => $"+{min} to Fire Skills",
            "ama" => $"+{min} to Amazon Skill Levels",
            "sor" => $"+{min} to Sorceress Skill Levels",
            "nec" => $"+{min} to Necromancer Skill Levels",
            "pal" => $"+{min} to Paladin Skill Levels",
            "bar" => $"+{min} to Barbarian Skill Levels",
            "dru" => $"+{min} to Druid Skill Levels",
            "ass" => $"+{min} to Assassin Skill Levels",
            "war" => $"+{min} to Warlock Skill Levels",
            _ => null, // state/fade/per-level/other -> not shown
        };
    }
}
