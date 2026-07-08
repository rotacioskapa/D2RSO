using D2RItemInspector.Model;

namespace D2RItemInspector.Report;

/// <summary>Renders an <see cref="InspectionResult"/> to the console — the program's standalone output.</summary>
public static class ConsoleReportRenderer
{
    public static void Print(InspectionResult result)
    {
        foreach (var character in result.Characters.OrderBy(kv => kv.Key).Select(kv => kv.Value))
            PrintCharacter(character);
        foreach (var stash in result.Stashes.OrderBy(kv => kv.Key).Select(kv => kv.Value))
            PrintStash(stash);
        PrintSummary(result);
    }

    private static void PrintCharacter(CharacterData c)
    {
        Console.WriteLine($"\n========== CHARACTER: {c.FileName} ==========");
        if (!c.Parsed)
        {
            Console.WriteLine(c.Error);
            return;
        }

        Console.WriteLine($"Name: {c.Name}   Class: {c.Class}   Level: {c.Level}   " +
                          $"Version: 0x{c.Version:X}   Expansion: {c.IsExpansion}");

        Console.WriteLine($"\n-- Inventory ({c.InventoryCount} items) --");
        PrintItems(c.Inventory);

        if (c.CorpseCount > 0)
        {
            Console.WriteLine($"\n-- Corpses ({c.CorpseCount}) --");
            PrintItems(c.Corpse);
        }
        if (c.Mercenary is { } merc)
        {
            Console.WriteLine($"\n-- Mercenary (id {merc.Id}, {merc.DeclaredCount} items) --");
            PrintItems(merc.Items);
        }
        if (c.Golem is { } golem)
        {
            Console.WriteLine($"\n-- Golem (exists: {golem.Exists}) --");
            if (golem.Item is { } item) PrintItem(item);
        }

        Console.WriteLine("\n[OK] parsed without errors.");
    }

    private static void PrintStash(StashData s)
    {
        Console.WriteLine($"\n========== SHARED STASH: {s.FileName} ({s.ByteLength} bytes) ==========");
        foreach (var tab in s.Tabs)
        {
            Console.WriteLine($"\n-- TAB {tab.Index} --  ({tab.DeclaredCount} items, {tab.Gold} gold)");
            PrintItems(tab.Items);
            if (tab.Error is not null) Console.WriteLine($"  {tab.Error}");
        }

        if (!s.Parsed)
            Console.WriteLine(s.Error);
        else
        {
            int total = s.Tabs.Sum(t => t.Items.Count);
            Console.WriteLine($"\n===== {s.TabCount} tabs, {total} items total =====");
        }
    }

    private static void PrintSummary(InspectionResult result)
    {
        int charsOk = result.Characters.Values.Count(c => c.Parsed);
        int stashesOk = result.Stashes.Values.Count(s => s.Parsed);
        Console.WriteLine("\n########## SUMMARY ##########");
        Console.WriteLine($"Characters:     {charsOk} parsed, {result.Characters.Count - charsOk} failed (of {result.Characters.Count})");
        Console.WriteLine($"Shared stashes: {stashesOk} parsed, {result.Stashes.Count - stashesOk} failed (of {result.Stashes.Count})");
    }

    private static void PrintItems(IEnumerable<ItemData> items)
    {
        foreach (var item in items) PrintItem(item);
    }

    private static void PrintItem(ItemData item, string indent = "  ")
    {
        string quantity = item.Quantity > 1 ? $"  x{item.Quantity}" : "";
        Console.WriteLine($"{indent}- {item.Name}{quantity}");
        foreach (var socket in item.Sockets)
            Console.WriteLine($"{indent}      └ {socket}");
    }
}
