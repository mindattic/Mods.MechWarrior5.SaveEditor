# Mods.MechWarrior5.SaveEditor

**MW5 Save Editor**: one app for **MechWarrior 5: Mercenaries** and **MechWarrior 5: Clans** saves.

Both games are edited through one interface styled after their own HUDs. Under it sits a lossless save
engine that never touches your original saves.

**It is a standalone program, not a mod the game loads:** one self-contained `MW5SaveEditor.exe`. Installed
with Vortex, it lands in Vortex's mod folder and the game ignores it; Vortex doesn't start it either. Open
that folder from Vortex (**Mods** list, right-click **MW5 Save Editor**, **Open in File Manager**) and run
`MW5SaveEditor.exe`, or add it as a Vortex **Tool** to launch it from the Dashboard. Without Vortex, extract
the zip anywhere and run the exe.

Player documentation is in
[docs/USER_GUIDE.md](docs/USER_GUIDE.md); the NexusMods listing text is in
[docs/NEXUS_PAGE.md](docs/NEXUS_PAGE.md).

## Features

### Both games

- **One window, two games.** The **MERCENARIES** and **CLANS** tabs in the title bar switch editors.
  Each tab keeps its own open save, so switching never loses edits. Opening a `.sav` file picks the
  right game by itself.
- **Pick saves by name.** Saves are listed by their in-game names, dates and money, read from the
  game's own index (`Campaign.json` for Mercenaries, `UserSaveMetaData` for Clans), not by GUID file
  names.
- **Batch everything.** A checkbox on every row. **Shift+click** ticks a range, **Ctrl+A** or **Select
  all** takes everything shown. One summary dialog covers each batch (done and skipped, with reasons).
- **Batch actions match the screen.** A row hidden by a search or filter is unticked, and batch buttons are
  disabled until something is ticked, so an action never touches rows you can't see.
- **Themes.** The look follows the game tab: Mercs (gunmetal and amber) for Mercenaries, Clans (cold steel
  and ice cyan) for Clans. Every page, dialog and the Mech Lab share one chamfered, HUD-styled frame built
  on MindAttic.Components.
- **Mech art** from Sarna.net, fetched once and cached. It can be switched off in Settings.

### MechWarrior 5: Mercenaries

- **Mechs.**
  - Every variant through **DLC 9: Ashes & Ascension** (filterable by class, hero, DLC, or owned DLC
    only), added to cold storage with real factory loadouts and paint.
  - Repair, reset to stock, or remove, in batches.
- **Mech Lab.** Hardpoint-aware weapon, equipment and armor refits, plus `.mw5loadout` export and
  import.
- **Company.** C-Bills, reputation, name, and campaign date (moving the date shifts payroll and every
  other dated event with it). Company export and import (`.mw5company`).
- **Inventory, pilots, factions.**
  - Inventory: bulk weapons, equipment and ammo at any tier.
  - Pilots: hire, release, persona, portrait, voice, bio, traits, skills, XP.
  - Factions: standing -100 to +100.
  - Mech traits and Cantina unlocks.
- **Game-data aware.** Reads the installed game and its enabled mods, so modded mechs show up. It
  warns before saving anything the game can't load.
- **Six languages:** English, Deutsch, Español, Français, 日本語, Русский.

### MechWarrior 5: Clans

- **Resources.** Kerenskies, Merits and Honor (Honor also moves the Honor level track), salvage
  components, crew, and per-chassis mech experience (MXP).
- **Mechs.** Repair (armor plus every installed part), duplicate with the same loadout, rename, and
  remove. Mechs that are deployed or in the mech bay's work queue are never removed.
- **Weapons and equipment.** Edit stock, requisition any item seen in Clans campaigns, add to or remove
  ticked items in bulk.
- **Progress.** Honor level, campaign cycles, and the research queue with completed research.

## Safety

The same rules apply to both games:

- **An original game save is never overwritten.** Saving one writes a new save file beside it, like
  the game's own. It is added to the game's load menu under the name you type (Mercs: `Campaign.json`;
  Clans: every rotating `UserSaveMetaData` file), and the original stays byte-for-byte identical.
- **Editor saves are stamped.** They carry one extra `Mech5SaveEditorStamp` property, which the game
  skips. Only stamped saves are updated in place, the previous version is kept as `.bak`, and the
  writer refuses any other target. A game re-save drops the stamp, so it is protected again.
- **Lossless or nothing.** Every save is parsed and re-serialized before editing. If the result isn't
  identical to the file on disk, the editor refuses to edit it. Every file written is read back before
  it replaces anything, and writes go through a temp file that is swapped in.
- **Index backups.** The load-menu index files are backed up (`.bak`) before each change.

## Building

Requires the .NET 10 SDK on Windows.

```powershell
git clone https://github.com/mindattic/Mods.MechWarrior5.SaveEditor.git
dotnet build MechWarrior5.SaveEditor.sln -c Release
dotnet run --project src/MechWarrior5.SaveEditor.App
```

| Project | What it is |
| --- | --- |
| `shared/src/MechWarrior5.Shared` | Lossless Unreal save engine: UE4 tagged properties (Mercs), and UE 5.4 type-name tags plus the `!GZIP` chunk archive and GVAS (Clans) |
| `shared/src/MechWarrior5.Shared.Wpf` | MW5 HUD theme, check-list multi-select, Sarna art, dialogs, settings |
| `src/MechWarrior5.Mercs.SaveEditor.Core` | MW5 Mercenaries save model, editors, catalogs, game-data scanner |
| `src/MechWarrior5.Clans.SaveEditor.Core` | MW5 Clans save model: inventory actors, mechs, progress, load-menu index |
| `src/MechWarrior5.SaveEditor.App` | The WPF/MVVM app (`MW5SaveEditor.exe`): one shell per game |
| `tests/` | NUnit tests (real-save tests are `[Explicit]` and read copies from `MW5_SAVE_DIR`) |

`shared/` holds the game-neutral projects both games use: `MechWarrior5.Shared` (the lossless Unreal
save engine, pak reading, the editor stamp) and `MechWarrior5.Shared.Wpf` (theme, dialogs, image
service), with their tests in `shared/tests`. They are ordinary projects of this repo; the former
Mods.MechWarrior5.Shared repo they came from is archived.

## Releasing

`/publish` builds `tools/package.ps1` (a self-contained, single-file `MW5SaveEditor.exe` zipped to
`dist/`), bumps the version, writes the changelog, and cuts a GitHub release. The release triggers
`.github/workflows/publish.yml`, which uploads the zip to the NexusMods page named in `nexus.json`.

## How the save formats work

- **Mercenaries** saves are header-less UE 4.26 tagged-property streams. `PersistentModelData` embeds
  every game model as an inline object. Mechs are `ActorSaveData` entries whose `ByteData` holds the
  `MechLoadout`.
- **Clans** saves are `!GZIP` Unreal compressed archives around UE 5.4 tagged properties: type-name
  trees and tag flags instead of UE4 tag data. Everything owned (currencies, salvage, crew, weapons,
  mechs, pilots) lives in `InventoryData` as `ActorSaveData` items. See
  [docs/research/clans-save-format.md](docs/research/clans-save-format.md).

## Changelog

### 2.0.0

A rebuilt interface and a long list of fixes from a full review of every feature in both games.

**Interface**
- Every page, dialog and the Mech Lab share one HUD-style look built on MindAttic.Components: chamfered
  panels, matching margins and spacing, and columns whose chips and buttons line up.
- The game picker in the title bar is one linked pair, MERCENARIES and CLANS; the look follows the game
  (amber for Mercenaries, ice blue for Clans). The title bar reads MechWarrior 5 // Save Editor.
- Batch buttons stay greyed out until something is ticked, and a search or filter unticks the rows it
  hides, so an action only ever touches what you can see.
- Empty pickers show a hint, every DLC code shows the DLC's name on hover, and dialog buttons for
  destructive actions are red.
- Everything is translated in all six languages (English, Deutsch, Español, Français, 日本語, Русский),
  including the Clans pages.
- Ctrl+S and Ctrl+O work anywhere in the window and include a value you're still typing.

**Mercenaries**
- The Mech Lab keeps a mech's damage when you apply a refit; use Repair to fix it.
- Mechs imported from a company package arrive fully repaired, and injured pilots arrive healthy.
- Mods that replace a mech's stock loadout are honored, a rescan forgets mods you've disabled, and a
  mod pilot voice shows instead of an empty box.
- Save As into another campaign lists the save with its own details.

**Clans**
- Remove mechs works (it was blocked by each mech's own mech bay plan), and the plan is removed with it;
  Duplicate gives every copy its own plan, and Rename renames it too.
- Repair restores destroyed body parts, and mechs missing one now show as DAMAGED.
- Saving keeps working when the game has a load-menu index file open.

**Safety**
- Save files and the game's index files are written to a temporary file first, so a crash or full disk
  can never leave them half-written; a read-only backup no longer blocks saving.
- A locked or damaged save in the folder no longer breaks the save list.

**Install**
- The download is a standalone program, not a mod the game loads. With Vortex, open its folder from
  Vortex's Mods list (Open in File Manager) and run MW5SaveEditor.exe, or add it as a Vortex tool.

### 1.0.0

First release: one save editor for MechWarrior 5: Mercenaries and Clans.

## Credits

- Inspired by JJ's [MW5 Merc Save Editor](https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466).
  No code from it is used; this editor was written from scratch.
- Mech art is fetched from [Sarna.net](https://www.sarna.net) at runtime and never bundled.
- The Rajdhani font is under the SIL OFL.
- Full notices, including data catalog licensing: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

This is an unofficial fan tool, not affiliated with Piranha Games.
