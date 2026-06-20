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

    // Internal/structural stats or durations shown elsewhere (or folded into another line).
    private static readonly HashSet<string> Skip = new()
    {
        "item_numsockets", "item_levelreq", "item_levelreqpct", "item_req_percent", "coldlength",
    };

    /// <summary>Stat list[0] = the item's own mods; for a set item, list[1..] are partial-set bonuses.</summary>
    public static List<StatLine> Format(Item item, Func<int, string?> skillName, string[][] skillTabs)
    {
        var result = new List<StatLine>();
        bool isSet = item.Quality == ItemQuality.Set;
        // Partial-set bonuses are stored in extra stat lists, one per set bit; bit b activates at
        // (b + 2) equipped set items (aprop1 -> 2 items, aprop2 -> 3 items, ...).
        var setBits = new List<int>();
        if (isSet)
            for (int b = 0; b < 8; b++)
                if ((item.SetItemMask & (1 << b)) != 0) setBits.Add(b);

        for (int i = 0; i < item.StatLists.Count; i++)
        {
            bool setBonus = isSet && i > 0;
            int items = setBonus && i - 1 < setBits.Count ? setBits[i - 1] + 2 : 0;
            foreach (var text in FormatList(item.StatLists[i].Stats, skillName, skillTabs))
                result.Add(new StatLine { Text = items > 0 ? $"{text} ({items} Items)" : text, Set = setBonus });
        }
        return result;
    }

    private static List<string> FormatList(List<ItemStat> stats, Func<int, string?> skillName, string[][] skillTabs)
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
        // +damage distinguishes min-only / max-only / both. Secondary (2-handed) and throw +damage
        // usually duplicate the primary, so the identical lines collapse via Distinct().
        AddDamage(lines, handled, byName, "mindamage", "maxdamage", "Damage");
        AddDamage(lines, handled, byName, "secondary_mindamage", "secondary_maxdamage", "Damage");
        AddDamage(lines, handled, byName, "item_throw_mindamage", "item_throw_maxdamage", "Damage");
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

        // Combine equal fire/cold/lightning/poison resist into one "All Resistances" line.
        if (byName.TryGetValue("fireresist", out int allres)
            && byName.TryGetValue("coldresist", out int cr) && cr == allres
            && byName.TryGetValue("lightresist", out int lr) && lr == allres
            && byName.TryGetValue("poisonresist", out int pr) && pr == allres)
        {
            lines.Add($"All Resistances {Plus(allres)}%");
            handled.Add("fireresist");
            handled.Add("coldresist");
            handled.Add("lightresist");
            handled.Add("poisonresist");
        }

        foreach (var s in stats)
        {
            if (handled.Contains(s.Stat) || Skip.Contains(s.Stat)) continue;
            if (FormatOne(s, skillName, skillTabs) is { } line) lines.Add(line);
        }
        return lines.Distinct().ToList();
    }

    // "Adds X-Y <label>" when both present; "+X to Minimum/Maximum <label>" when only one side is.
    private static void AddDamage(List<string> lines, HashSet<string> handled, Dictionary<string, int> b,
        string min, string max, string label)
    {
        if (!b.ContainsKey(min) && !b.ContainsKey(max)) return;
        handled.Add(min);
        handled.Add(max);
        int mn = b.GetValueOrDefault(min), mx = b.GetValueOrDefault(max);
        if (mn != 0 && mx != 0) lines.Add($"Adds {mn}-{mx} {label}");
        else if (mx != 0) lines.Add($"+{mx} to Maximum {label}");
        else if (mn != 0) lines.Add($"+{mn} to Minimum {label}");
    }

    private static string? FormatOne(ItemStat s, Func<int, string?> skillName, string[][] skillTabs)
    {
        int v = s.Value;
        string Skill(int? id) => id is int i ? (skillName(i) ?? $"Skill #{i}") : "Skill";

        // +X to a specific class skill tree. Library field names are misleading: SkillLevel = class id,
        // SkillTab = local tab (0-2), Value = the actual +levels.
        if (s.Stat == "item_addskill_tab")
        {
            int cls = s.SkillLevel ?? 0, tab = s.SkillTab ?? 0;
            string tabName = cls < skillTabs.Length && tab is >= 0 and < 3 ? skillTabs[cls][tab] : "Skill";
            return $"+{v} to {tabName} ({ClassName(cls)} Only)";
        }
        // +X to <element> Skills — the element is in Param (1=Fire confirmed; rest best-effort).
        if (s.Stat == "item_elemskill")
            return $"+{v} to {ElemName(s.Param)} Skills";

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
            "item_lightradius" => $"{Plus(v)} to Light Radius",
            "item_halffreezeduration" => "Half Freeze Duration",
            "item_demon_tohit" => $"+{v} to Attack Rating against Demons",
            "item_undead_tohit" => $"+{v} to Attack Rating against Undead",
            "maxdurability" => $"+{v} Maximum Durability",
            "item_maxdurability_percent" => $"Increase Maximum Durability {v}%",
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
            // These have Encode=1, so the skill id is in Param (not SkillId) and the level is Value.
            "item_singleskill" or "item_nonclassskill" or "item_oskill" => $"+{v} to {Skill(s.Param)}",
            "item_charged_skill" => $"Level {s.SkillLevel} {Skill(s.SkillId)} ({v}/{s.MaxCharges} Charges)",
            "item_skillonattack" or "item_skillonhit" or "item_skillonkill" or "item_skillondeath" or "item_skillongethit"
                => $"{v}% Chance to cast Level {s.SkillLevel} {Skill(s.SkillId)}",
            "armorclass_vs_missile" => $"+{v} Defense vs. Missile",
            "armorclass_vs_hth" => $"+{v} Defense vs. Melee",
            "item_fractionaltargetac" or "item_ignoretargetac" => "Ignore Target's Defense",
            "item_absorbmagic" => $"Magic Absorb +{v}",
            "item_absorbfire" => $"Fire Absorb +{v}",
            "item_absorbcold" => $"Cold Absorb +{v}",
            "item_absorblight" => $"Lightning Absorb +{v}",
            "item_absorbmagic_percent" => $"Magic Absorb {v}%",
            "item_absorbfire_percent" => $"Fire Absorb {v}%",
            "item_absorbcold_percent" => $"Cold Absorb {v}%",
            "item_absorblight_percent" => $"Lightning Absorb {v}%",
            "item_absorb_cold_perlevel" => PerLevel(v, "Cold Absorb"),
            "item_absorb_fire_perlevel" => PerLevel(v, "Fire Absorb"),
            "item_absorb_ltng_perlevel" => PerLevel(v, "Lightning Absorb"),
            "item_absorb_pois_perlevel" => PerLevel(v, "Poison Absorb"),
            "item_freeze" => $"Freezes Target +{v}",
            "item_pierce" => $"{v}% Piercing Attack",
            "item_knockback" => "Knockback",
            "toblock" => $"+{v}% Increased Chance to Block",
            "item_attackertakeslightdamage" => $"Attacker Takes Lightning Damage of {v}",
            "item_stupidity" => "Hit Blinds Target",
            "item_reducedprices" => $"Reduces all Vendor Prices {v}%",
            "passive_fire_mastery" => $"+{v}% to Fire Skill Damage",
            "passive_ltng_mastery" => $"+{v}% to Lightning Skill Damage",
            "passive_cold_mastery" => $"+{v}% to Cold Skill Damage",
            "passive_pois_mastery" => $"+{v}% to Poison Skill Damage",
            "passive_mag_mastery" => $"+{v}% to Magic Skill Damage",
            "passive_fire_pierce" => $"-{v}% to Enemy Fire Resistance",
            "passive_cold_pierce" => $"-{v}% to Enemy Cold Resistance",
            "passive_ltng_pierce" => $"-{v}% to Enemy Lightning Resistance",
            "passive_pois_pierce" => $"-{v}% to Enemy Poison Resistance",
            "passive_mag_pierce" => $"-{v}% to Enemy Magic Resistance",
            "item_poisonlengthresist" => $"Poison Length Reduced by {v}%",
            "item_healafterdemonkill" => $"+{v} Life after each Demon Kill",
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
    private static string ElemName(int? p) => p switch
    {
        1 => "Fire", 2 => "Lightning", 3 => "Cold", 4 => "Poison", 5 => "Magic", _ => "Elemental",
    };
}
