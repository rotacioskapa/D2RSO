using D2SLib.Model.Save;

namespace D2RItemInspector;

/// <summary>
/// Turns an item's raw magical stats into readable mod lines for the hover tooltip. Best-effort:
/// common numeric/percentage mods are formatted accurately; individual skill bonuses can't show the
/// skill's name (skills.txt/string tables aren't bundled) so they fall back to a skill id.
/// Stats from a set item's extra stat lists are flagged as set bonuses (rendered green).
/// </summary>
public static class StatFormatter
{
    private static readonly string[] Classes =
        { "Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin", "Warlock" };

    // Per-class skill-tree (tab) names, indexed [classId][localTab 0-2]. Best-effort standard order.
    private static readonly string[][] SkillTabs =
    {
        new[] { "Bow and Crossbow", "Passive and Magic", "Javelin and Spear" }, // Amazon
        new[] { "Fire", "Lightning", "Cold" },                                   // Sorceress
        new[] { "Curses", "Poison and Bone", "Summoning" },                      // Necromancer
        new[] { "Combat", "Offensive Auras", "Defensive Auras" },                // Paladin
        new[] { "Combat", "Masteries", "Warcries" },                             // Barbarian
        new[] { "Summoning", "Shape Shifting", "Elemental" },                    // Druid
        new[] { "Traps", "Shadow Disciplines", "Martial Arts" },                 // Assassin
    };

    // Internal/structural stats or durations shown elsewhere (or folded into another line).
    private static readonly HashSet<string> Skip = new()
    {
        "item_numsockets", "item_levelreq", "item_levelreqpct", "item_req_percent", "coldlength",
    };

    /// <summary>Stat list[0] = the item's own mods; for a set item, list[1..] are partial-set bonuses.</summary>
    public static List<StatLine> Format(Item item)
    {
        var result = new List<StatLine>();
        bool isSet = item.Quality == ItemQuality.Set;
        for (int i = 0; i < item.StatLists.Count; i++)
        {
            bool setBonus = isSet && i > 0;
            foreach (var text in FormatList(item.StatLists[i].Stats))
                result.Add(new StatLine { Text = text, Set = setBonus });
        }
        return result;
    }

    private static List<string> FormatList(List<ItemStat> stats)
    {
        var byName = new Dictionary<string, int>();
        foreach (var s in stats) byName[s.Stat] = s.Value;

        var lines = new List<string>();
        var handled = new HashSet<string>();

        if (byName.TryGetValue("item_maxdamage_percent", out int ed) || byName.TryGetValue("item_mindamage_percent", out ed))
        {
            lines.Add($"+{ed}% Enhanced Damage");
            handled.Add("item_maxdamage_percent");
            handled.Add("item_mindamage_percent");
        }
        AddDamage(lines, handled, byName, "mindamage", "maxdamage", "Damage");
        AddDamage(lines, handled, byName, "firemindam", "firemaxdam", "Fire Damage");
        AddDamage(lines, handled, byName, "coldmindam", "coldmaxdam", "Cold Damage");
        AddDamage(lines, handled, byName, "lightmindam", "lightmaxdam", "Lightning Damage");
        AddDamage(lines, handled, byName, "magicmindam", "magicmaxdam", "Magic Damage");
        if (byName.TryGetValue("poisonmindam", out int pmin))
        {
            int frames = byName.GetValueOrDefault("poisonlength", 0);
            int total = (int)((long)Math.Max(pmin, byName.GetValueOrDefault("poisonmaxdam", pmin)) * Math.Max(frames, 1) / 256);
            int secs = frames / 25;
            lines.Add(secs > 0 ? $"+{total} Poison Damage over {secs} Seconds" : $"+{total} Poison Damage");
            handled.Add("poisonmindam");
            handled.Add("poisonmaxdam");
            handled.Add("poisonlength");
        }

        foreach (var s in stats)
        {
            if (handled.Contains(s.Stat) || Skip.Contains(s.Stat)) continue;
            if (FormatOne(s) is { } line) lines.Add(line);
        }
        return lines.Distinct().ToList();
    }

    private static void AddDamage(List<string> lines, HashSet<string> handled, Dictionary<string, int> b,
        string min, string max, string label)
    {
        if (!b.ContainsKey(min) && !b.ContainsKey(max)) return;
        int lo = b.GetValueOrDefault(min), hi = b.GetValueOrDefault(max, lo);
        lines.Add(lo == hi ? $"+{hi} {label}" : $"Adds {lo}-{hi} {label}");
        handled.Add(min);
        handled.Add(max);
    }

    private static string? FormatOne(ItemStat s)
    {
        int v = s.Value;

        // +X to a specific class skill tree. Library field names are misleading: SkillLevel = class id,
        // SkillTab = local tab (0-2), Value = the actual +levels.
        if (s.Stat == "item_addskill_tab")
        {
            int cls = s.SkillLevel ?? 0, tab = s.SkillTab ?? 0;
            string tabName = cls < SkillTabs.Length && tab is >= 0 and < 3 ? SkillTabs[cls][tab] : "Skill";
            return $"+{v} to {tabName} ({ClassName(cls)} Only)";
        }

        return s.Stat switch
        {
            "strength" => $"+{v} to Strength",
            "energy" => $"+{v} to Energy",
            "dexterity" => $"+{v} to Dexterity",
            "vitality" => $"+{v} to Vitality",
            "maxhp" => $"+{v} to Life",
            "maxmana" => $"+{v} to Mana",
            "maxstamina" => $"+{v} to Stamina",
            "item_maxmana_percent" => $"Increase Maximum Mana {v}%",
            "item_maxhp_percent" => $"Increase Maximum Life {v}%",
            "hpregen" => $"Replenish Life +{v}",
            "manarecoverybonus" => $"Regenerate Mana {v}%",
            "staminarecoverybonus" => $"Heal Stamina Plus {v}%",
            "item_manaafterkill" => $"+{v} to Mana after each Kill",
            "item_healafterkill" => $"+{v} to Life after each Kill",
            "item_damagetomana" => $"{v}% Damage Taken Goes to Mana",
            "armorclass" => $"+{v} Defense",
            "item_armor_percent" => $"+{v}% Enhanced Defense",
            "tohit" => $"+{v} to Attack Rating",
            "item_tohit_percent" => $"+{v}% Bonus to Attack Rating",
            "item_demondamage_percent" => $"+{v}% Damage to Demons",
            "item_undeaddamage_percent" => $"+{v}% Damage to Undead",
            "item_fastercastrate" => $"+{v}% Faster Cast Rate",
            "item_fasterattackrate" => $"+{v}% Increased Attack Speed",
            "item_fastergethitrate" => $"+{v}% Faster Hit Recovery",
            "item_fasterblockrate" => $"+{v}% Faster Block Rate",
            "item_fastermovevelocity" => $"+{v}% Faster Run/Walk",
            "item_addexperience" => $"+{v}% to Experience Gained",
            "item_goldbonus" => $"+{v}% Extra Gold from Monsters",
            "item_magicbonus" => $"{Plus(v)}% Better Chance of Getting Magic Items",
            "fireresist" => $"Fire Resist {Plus(v)}%",
            "coldresist" => $"Cold Resist {Plus(v)}%",
            "lightresist" => $"Lightning Resist {Plus(v)}%",
            "poisonresist" => $"Poison Resist {Plus(v)}%",
            "maxfireresist" => $"+{v}% to Maximum Fire Resist",
            "maxcoldresist" => $"+{v}% to Maximum Cold Resist",
            "maxlightresist" => $"+{v}% to Maximum Lightning Resist",
            "maxpoisonresist" => $"+{v}% to Maximum Poison Resist",
            "normal_damage_reduction" => $"Damage Reduced by {v}",
            "magic_damage_reduction" => $"Magic Damage Reduced by {v}",
            "damageresist" => $"Damage Reduced by {v}%",
            "magicresist" => $"Magic Resist +{v}%",
            "item_lightradius" => $"+{v} to Light Radius",
            "item_attackertakesdamage" => $"Attacker Takes Damage of {v}",
            "item_replenish_durability" => "Replenishes Durability",
            "item_replenish_quantity" => "Replenishes Quantity",
            "item_indesctructible" => "Indestructible",
            "lifedrainmindam" => $"{v}% Life Stolen per Hit",
            "manadrainmindam" => $"{v}% Mana Stolen per Hit",
            "item_crushingblow" => $"{v}% Chance of Crushing Blow",
            "item_deadlystrike" => $"{v}% Deadly Strike",
            "item_openwounds" => $"{v}% Chance of Open Wounds",
            "item_cannotbefrozen" => "Cannot Be Frozen",
            "item_allskills" => $"+{v} to All Skills",
            "item_addclassskills" => $"+{v} to {ClassName(s.Param)} Skill Levels",
            "item_singleskill" or "item_nonclassskill" or "item_oskill" => $"+{s.SkillLevel ?? v} to Skill #{s.SkillId}",
            "item_charged_skill" => $"Level {s.SkillLevel} Skill #{s.SkillId} ({v}/{s.MaxCharges} Charges)",
            "item_skillonattack" or "item_skillonhit" or "item_skillonkill" or "item_skillondeath" or "item_skillongethit"
                => $"{v}% Chance to cast Level {s.SkillLevel} Skill #{s.SkillId}",
            "armorclass_vs_missile" => $"+{v} Defense vs. Missile",
            "armorclass_vs_hth" => $"+{v} Defense vs. Melee",
            "item_fractionaltargetac" => "Ignore Target's Defense",
            "item_absorbmagic" => $"Magic Absorb +{v}",
            "item_absorbfire" => $"Fire Absorb +{v}",
            "item_absorbcold" => $"Cold Absorb +{v}",
            "item_absorblight" => $"Lightning Absorb +{v}",
            "item_absorbmagic_percent" => $"Magic Absorb {v}%",
            "item_absorbfire_percent" => $"Fire Absorb {v}%",
            "item_absorbcold_percent" => $"Cold Absorb {v}%",
            "item_absorblight_percent" => $"Lightning Absorb {v}%",
            "item_hp_perlevel" => PerLevel(v, "to Life"),
            "item_mana_perlevel" => PerLevel(v, "to Mana"),
            "item_armor_perlevel" => PerLevel(v, "Defense"),
            "item_strength_perlevel" => PerLevel(v, "to Strength"),
            "item_dexterity_perlevel" => PerLevel(v, "to Dexterity"),
            "item_energy_perlevel" => PerLevel(v, "to Energy"),
            "item_vitality_perlevel" => PerLevel(v, "to Vitality"),
            "item_maxdamage_perlevel" => PerLevel(v, "to Maximum Damage"),
            "item_tohit_perlevel" => PerLevel(v, "to Attack Rating"),
            "item_find_magic_perlevel" => PerLevel(v, "% Better Chance of Magic Items"),
            "item_find_gold_perlevel" => PerLevel(v, "% Extra Gold from Monsters"),
            _ => Humanize(s.Stat, v),
        };
    }

    // Per-level stats are stored as (rate * 8); show the per-character-level rate.
    private static string PerLevel(int v, string what) =>
        $"+{(v / 8.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {what} (Based on Character Level)";

    // Readable fallback for an unmapped stat (e.g. "item_some_stat" -> "Some Stat: +3").
    private static string? Humanize(string stat, int v)
    {
        if (v == 0) return null;
        string label = stat.StartsWith("item_") ? stat[5..] : stat;
        label = label.Replace('_', ' ');
        if (label.Length > 0) label = char.ToUpper(label[0]) + label[1..];
        return $"{label}: {Plus(v)}";
    }

    private static string Plus(int v) => v >= 0 ? $"+{v}" : v.ToString();
    private static string ClassName(int? id) => id is >= 0 and < 8 ? Classes[id.Value] : "Class";
}
