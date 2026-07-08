# Changelog

Notable changes per release. Each `## v<x.y.z>` section is used **verbatim** as the GitHub Release
description when the matching `v<x.y.z>` tag is pushed (see `.github/workflows/release.yml`). To cut a
release: add a new section at the top, then push the tag. A trailing ` — <date>` on the heading is
fine (e.g. `## v1.1.0 — 2026-08-01`).

## v1.0.0

Browse and filter **every item** across all your offline Diablo II: Resurrected characters and shared
stash in one self-contained web page. Supports the **Return of the Warlock** expansion (including the
new Warlock class) as well as vanilla D2R.

### Download & run
- Download **`D2RItemInspector.exe`** below — a single self-contained Windows (x64) executable, **no
  .NET install required**.
- Double-click it. It reads your live save folder
  (`%USERPROFILE%\Saved Games\Diablo II Resurrected`), writes `items.html` next to the exe, and opens
  it in your browser.
- Tip: exit D2R to the main menu first (so the game flushes your saves), then run/refresh to see your
  current gear.

### Features
A sortable, filterable report of all your equipment (weapons, armor, jewelry, charms, jewels) across
every character — inventory, stash, corpse, mercenary and iron golem — plus the shared stash.

- **Rarity-colored names** (runewords shown by name) and sortable columns: type, base, quality, set,
  sockets, required level/str/dex, usable class, ethereal, and location.
- **Hover tooltips:**
  - Item stats formatted like in-game (skills & skill tabs, resistances, per-level bonuses, auras, …).
  - Set tooltip — an *(x / n Items)* collection counter, a **Missing:** piece list, and set bonuses
    grouped under *2 / 3 / … Item Bonuses* and *Full Set Bonuses*.
  - Socket contents, with open sockets marked *empty*.
- **Wiki links** — unique/set/runeword names link to their diablo.fandom.com page (each verified to
  exist before linking).
- **Filters** (all combine): name search, rarity, type, base quality, item **features** (resistances,
  crushing blow, faster cast rate, +attributes, indestructible, …), usable-by-class, location, max
  required level, and toggles for runewords-only / ethereal-only / empty-sockets / duplicates.
- **Click a set name** to filter the list to just that set.

### Compatibility
- Return of the Warlock expansion (save version `0x69`, Warlock class) **and** pre-RotW vanilla D2R
  (`0x62`). Offline single-player saves.

### Notes
- Self-contained ~37 MB exe; the wiki-link cache is stored in `%LOCALAPPDATA%\D2RItemInspector`.
- Because RotW is its own expansion, some item stats intentionally differ from vanilla D2 (and the
  vanilla-focused wiki) — those differences are expected.
