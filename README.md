# D2RItemInspector

Get the list of all items of your offline characters (along with shared stash), which you can filter in many ways. Works with RotW characters.

## What it does

Parses Diablo II: Resurrected save files and prints every item it finds:

- **Characters** (`.d2s`) — inventory, corpse, mercenary and golem items, with resolved names (unique/set/runeword/magic/rare), ethereal/socket annotations, and socketed runes/gems.
- **Shared stash** (`.d2i`) — every tab, with stack quantities.
- Supports both **RotW** (Reign of the Wolf mod, save version `0x69`, including the new **Warlock** class) and **pre-RotW** vanilla D2R characters (version `0x62`).

## Requirements

- **.NET 10 SDK** — to build/run the app (`D2RItemInspector.csproj` targets `net10.0`).
- **.NET 6 SDK** — to build the bundled `D2SLib` library (targets `net6.0`).

Check what you have with `dotnet --list-sdks`.

## Build & run

> **Important:** the app references the `D2SLib` library as a prebuilt DLL (via `HintPath`), so you **must build the library in Release first**. The app won't compile until that DLL exists.

```sh
# 1. Build the library (Release, net6.0)
dotnet build D2SLib-D2R/src/D2SLib.csproj -c Release -f net6.0

# 2. Build & run the app
dotnet run -c Release
```

If you edit anything under `D2SLib-D2R/src`, re-run step 1 before re-running the app (the DLL is not rebuilt automatically).

## Providing your own saves

The app reads **every** `.d2s` and `.d2i` file in `etc/SavedGames/`. Copy your saves there from the D2R save folder:

```
%USERPROFILE%\Saved Games\Diablo II Resurrected
```

Each file is parsed independently — if one save can't be read it's reported and the run continues.

## Output

For each character and stash, the program prints a header followed by the items, e.g.:

```
========== CHARACTER: Elza.d2s ==========
Name: Elza   Class: Sorceress   Level: 90   Version: 0x69   Expansion: True

-- Inventory (64 items) --
  - Spirit (Monarch) <4 sock>
        └ Tal Rune
        └ Thul Rune
        └ Ort Rune
        └ Amn Rune
  ...
-- Mercenary (id 2891915263, 3 items) --
  - Insight (Colossus Voulge) <4 sock>
  ...

========== SHARED STASH: ModernSharedStashSoftCoreV2.d2i (7812 bytes) ==========
-- TAB 5 --  (79 items, 0 gold)
  - Tal Rune  x25
  ...

########## SUMMARY ##########
Characters:     14 parsed, 0 failed (of 14)
Shared stashes: 2 parsed, 0 failed (of 2)
```

## Using it from another app

Item collection is separate from the console output, so you can consume the data directly. `SaveInspector.Collect()` returns an `InspectionResult` — dictionaries of `CharacterData` / `StashData` keyed by file name:

```csharp
var inspector = new SaveInspector(@"etc\SavedGames", @"D2SLib-D2R\src\Resources");
InspectionResult result = inspector.Collect();

CharacterData elza = result.Characters["Elza.d2s"];
foreach (ItemData item in elza.Inventory)
    Console.WriteLine($"{item.Name} x{item.Quantity}");
```

The standalone program is just `SaveInspector.Collect()` followed by `ConsoleReportRenderer.Print(result)`.

## Project layout

| Path | Purpose |
|------|---------|
| `Program.cs` | Standalone entry point (collect + print) |
| `SaveInspector.cs` | Public API: scans the save dir, returns `InspectionResult` |
| `InspectionModel.cs` | Data model (`CharacterData`, `StashData`, `ItemData`, …) |
| `CharacterInspector.cs` / `SharedStashInspector.cs` | Read one `.d2s` / `.d2i` into data |
| `ItemNameResolver.cs` / `ItemMapping.cs` | Name resolution and item mapping |
| `ConsoleReportRenderer.cs` | Renders an `InspectionResult` to the console |
| `D2SLib-D2R/` | Vendored, modified [D2SLib](https://github.com/locbones/D2SLib-D2R) with RotW + pre-RotW parsing support |

### Notes

- RotW and pre-RotW saves use different game data tables, so the library embeds both sets and picks the right one per save version (RotW = `Resources/*.txt`, pre-RotW = `Resources/Legacy_*.txt`).
- Pre-RotW item base names may be spelled slightly differently (e.g. *"Charm Small"* vs *"Small Charm"*) — that's just the older data table's text, not a parsing error.
