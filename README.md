# Mods.MechWarrior5.SaveEditor

**MW5 Save Editor**: one app for **MechWarrior 5: Mercenaries** and **MechWarrior 5: Clans** saves.

Both games are edited through one interface styled after their own HUDs. Under it sits a lossless save
engine that never touches your original saves.

**It is a standalone program, not a mod the game loads:** a self-contained folder whose `MW5SaveEditor.exe` you run
yourself (the .NET runtime comes with it). It never connects to the internet. Download it from either NexusMods page or from GitHub (see Download
below); Vortex isn't needed.

Player documentation is in
[docs/USER_GUIDE.md](docs/USER_GUIDE.md); the NexusMods listing text is in
[docs/NEXUS_PAGE.md](docs/NEXUS_PAGE.md).

## Download

Download `MechWarrior5.SaveEditor-<version>.zip`. The same file is in every place:

- **NexusMods** (main file):
  [MechWarrior 5: Mercenaries](https://www.nexusmods.com/mechwarrior5mercenaries/mods/1677),
  [MechWarrior 5: Clans](https://www.nexusmods.com/mechwarrior5clans/mods/70)
- **GitHub** (mirror and source): [latest release](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/releases/latest)
  ([all releases](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/releases))

Windows 10 or 11, 64-bit; no .NET install needed. Extract it anywhere and run `MW5SaveEditor.exe`. Full steps are in the [user guide](docs/USER_GUIDE.md#install).

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
- **'Mech pictures from your own game.** Mercenaries pictures are read from the installed game's files
  (base game, DLC and mods). Clans pictures are read from the installed Clans files, which the game keeps
  Oodle-compressed: the editor uses an Oodle DLL (`oo2core*.dll`, Epic's decompression library) that is
  already on your PC, for example inside an Unreal Engine install, and never downloads or bundles one.
  Without one, Clans 'Mechs use the Mercenaries picture of the same chassis.
- **No network access.** The program never connects to the internet. It reads only your save files and your
  game's and mods' files, and writes only saves and `settings.json`.
- **Sarna.net links.** Every 'Mech links to its page on Sarna.net, the BattleTech wiki with the fullest technical
  specs: the link icon in the requisition list, or VIEW ON SARNA.NET under any enlarged picture. Clicking opens
  the page in your own browser; the editor itself downloads nothing.

### MechWarrior 5: Mercenaries

- **Mechs.**
  - Every variant through **DLC 9: Ashes & Ascension** (filterable by class, hero, DLC, or owned DLC
    only), added to cold storage with real factory loadouts and paint.
  - Repair, reset to stock, or remove, in batches.
- **Mech Lab.** Hardpoint-aware weapon, equipment and armor refits, plus `.mw5loadout` export and
  import.
- **Company.** C-Bills, reputation, name, and campaign date (moving the date shifts payroll and every
  other dated event with it). Company export and import (`.mw5company`).
  - Cantina career paths: points per path with the level reached and the next level's 'Mech trait reward,
    read from the game's (and mods') career path data. The game offers each reached level's reward in the
    Cantina, as usual.
  - Arena fame (DLC 6): fame points and rank, with the ranks read from the game's fame table.
- **Inventory, pilots, factions.**
  - Inventory: bulk weapons, equipment and ammo at any tier, optionally with each weapon's ammo added too
    (paired by the game's own ammo types, mods included).
  - Pilots: hire (with the selected pilot's skills, as a rookie with no experience, or with random skills
    within your roster's range), release, persona, portrait, voice, bio, traits, skills, XP.
  - Factions: standing -100 to +100.
  - Mech traits and Cantina unlocks.
- **Follows your mods.** Reads every enabled mod (Mods folder and Steam Workshop, in the game's load order)
  straight from its files: weapons, equipment, ammo, pilot and 'Mech traits, 'Mechs, and the pilot skill
  curve. A mod that raises the skill cap (such as Pilot Overhaul) raises it in the editor too. It warns
  before saving anything the game can't load.
- **Add-on packs.** Optional JSON files in the `Addons` folder carry a mod's values (limits, names,
  additions). Each applies only while its mod is installed, enabled and a version it lists; packs for AVTA,
  Pilot Overhaul - Eternal, YAW-CE, the YASV patch and YAML ship with the editor. Format:
  [docs/ADDONS.md](docs/ADDONS.md).
- **Compatibility report.** Opening a save lists everything in it that differs from the vanilla game, and
  the mod behind each item with its version and page, or why that mod isn't active (missing, disabled,
  wrong version).
- **Six languages:** English, Deutsch, Español, Français, 日本語, Русский.

### MechWarrior 5: Clans

- **Resources.** Kerenskies, Merits and Honor (Honor also moves the Honor level track), salvage
  components, crew, and per-chassis mech experience (MXP).
- **Mechs.** Repair (armor plus every installed part), duplicate with the same loadout, rename, and
  remove. Mechs that are deployed or in the mech bay's work queue are never removed.
- **Weapons and equipment.** Edit stock, requisition any item seen in Clans campaigns, add to or remove
  ticked items in bulk.
- **Progress.** Honor level, campaign cycles, and the research queue with completed research. Honor levels
  follow the game's own Honor ladder, read from its files when an Oodle DLL is on your PC (the built-in ladder
  otherwise).
- **Compatibility report.** Opening a save lists every asset it uses that the base game doesn't have, and the
  installed Clans mod that ships it (on or off), with its version and page. Settings lists the installed Clans
  mods.

## How mod support works

- **Every installed mod is read from its own files at startup.** The editor has no per-mod code: whatever the
  game's data says (items, traits, 'Mechs, skill curve, career paths, ammo types, fame ranks), it uses. A new
  or updated mod that uses the game's own data formats is followed without an editor update; rescan in
  Settings after changing mods.
- **Add-on packs fix or tweak one mod.** When a mod's own data is wrong or missing something (an ammo type
  that doesn't pair, a label, a limit the data doesn't state), a small JSON pack in the `Addons` folder
  corrects it while that mod is active. Report a problem with a mod on
  [GitHub issues](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/issues); the pack format is
  in [docs/ADDONS.md](docs/ADDONS.md).
- **What it can't follow.** A mod that changes rules only in Blueprint code leaves no data to read. Clans mods
  are recognized by name and traced in the compatibility report; their detailed stats aren't read yet (Clans
  stores them in unversioned data). Chain fire, the quest log, and moving 'Mechs in or out of the active mech
  bay aren't offered: their save layouts couldn't be verified, and the game does them itself.

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
| `shared/src/MechWarrior5.Shared.Wpf` | MW5 HUD theme, check-list multi-select, 'Mech picture behavior and lightbox, dialogs, settings |
| `src/MechWarrior5.Mercs.SaveEditor.Core` | MW5 Mercenaries save model, editors, catalogs, game-data scanner |
| `src/MechWarrior5.Clans.SaveEditor.Core` | MW5 Clans save model: inventory actors, mechs, progress, load-menu index |
| `src/MechWarrior5.SaveEditor.App` | The WPF/MVVM app (`MW5SaveEditor.exe`): one shell per game |
| `tests/` | NUnit tests (real-save tests are `[Explicit]` and read copies from `MW5_SAVE_DIR`) |

`shared/` holds the game-neutral projects both games use: `MechWarrior5.Shared` (the lossless Unreal
save engine, pak reading, texture decoding, the editor stamp) and `MechWarrior5.Shared.Wpf` (theme,
dialogs, the 'Mech picture behavior), with their tests in `shared/tests`. They are ordinary projects of this repo; the former
Mods.MechWarrior5.Shared repo they came from is archived.

## Releasing

`/publish` builds `tools/package.ps1` (a self-contained folder, `MW5SaveEditor.exe` beside the .NET runtime
DLLs, zipped to `dist/`), bumps the version, writes the changelog, and cuts a GitHub release. The release triggers
`.github/workflows/publish.yml`, which uploads that same zip, fetched from the release, to both NexusMods
pages in `nexus.json`. `tools/AddonPack` generates add-on packs from a mod's files (see
[docs/ADDONS.md](docs/ADDONS.md)).

## How the save formats work

- **Mercenaries** saves are header-less UE 4.26 tagged-property streams. `PersistentModelData` embeds
  every game model as an inline object. Mechs are `ActorSaveData` entries whose `ByteData` holds the
  `MechLoadout`.
- **Clans** saves are `!GZIP` Unreal compressed archives around UE 5.4 tagged properties: type-name
  trees and tag flags instead of UE4 tag data. Everything owned (currencies, salvage, crew, weapons,
  mechs, pilots) lives in `InventoryData` as `ActorSaveData` items. See
  [docs/research/clans-save-format.md](docs/research/clans-save-format.md).

## Changelog

### Unreleased

**Company page** (Mercenaries)
- Cantina career paths: points per path, the level reached and the next level's reward, with Next level, Max
  and Max all. Paths and levels come from the game's and enabled mods' data.
- Arena fame (DLC 6): points and rank, with Next rank and Max; ranks come from the game's fame table.

**Inventory and pilots** (Mercenaries)
- Requisition can add each picked weapon's ammo too, paired by the game's own ammo types (mods included).
- Hire a recruit with the selected pilot's skills, as a rookie (no experience), or with random skills within
  your roster's range.

**Clans**
- Honor levels follow the game's own Honor ladder, read from its files.
- Compatibility report: assets a save uses that the base game lacks, traced to the installed Clans mod (on or
  off) with its version and page; Settings lists the installed Clans mods.

**Both games**
- Every 'Mech links to its Sarna.net page (link icon in the requisition list, VIEW ON SARNA.NET under an
  enlarged picture); it opens in your browser only when you click. Nothing is downloaded.
- The download is now a folder: `MW5SaveEditor.exe` beside the .NET runtime files it needs, instead of one
  large single-file exe (that format can trip antivirus heuristics). Keep the files together.
- Add-on packs can correct a mod's ammo pairing (`ammoType`).

### 3.0.0

**No network access**
- The editor never connects to the internet. 'Mech pictures no longer come from Sarna.net: they are read
  from your installed MechWarrior 5: Mercenaries files, so every variant, DLC and modded 'Mech gets the
  game's own picture.
- Clans 'Mechs show Clans' own pictures (all 30 chassis) when an Oodle DLL is already on your PC; the
  editor finds it, or you choose it in Clans Settings. It is never downloaded or bundled. Without one,
  Clans uses the Mercenaries picture of the same chassis.
- The picture download setting, the picture cache and the Sarna links are gone.

**Mods** (Mercenaries)
- Every enabled mod is read from its own files, from the Mods folder and from Steam Workshop, in the game's
  load order: its weapons, equipment and ammo (with their in-game names, hardpoints and slots), pilot and
  'Mech traits, 'Mechs and their pictures. Mod items show their mod's name in the pickers.
- A mod's pilot skill curve sets the skill cap: with Pilot Overhaul - Eternal enabled, caps go to 30. Every
  limit (skill cap, experience, salary, C-Bills, faction standing, stack size) follows the active mods, and a
  value already beyond a limit is kept as it is.
- Add-on packs: JSON files in the new `Addons` folder carry a mod's values. Packs for Additional Vanilla
  Traits and Academies, PilotOverhaul - Eternal, Yet Another Weapon - Complete Edition, the YASV Complete
  Edition Patch and Yet Another Mechlab are included. Settings shows each pack's status (active, mod not
  installed, mod disabled, version not supported) with a switch, and a switch to turn them all off.

**Compatibility report**
- Opening a save checks it against the installed game and mods. Settings, Compatibility lists everything
  that differs from vanilla: content from mods and values past vanilla limits. Each line names the mod,
  its version and its page, or says why that mod isn't active (not installed, disabled in the game, wrong
  version). The load message says when something needs a mod that isn't active.

**Install**
- The program is the main file on both NexusMods pages again; GitHub releases carry the same zip.

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
- The editor downloads from GitHub releases; the NexusMods pages link there. It's a standalone program:
  extract the zip anywhere and run MW5SaveEditor.exe.

### 1.0.0

First release: one save editor for MechWarrior 5: Mercenaries and Clans.

## Credits

- Inspired by JJ's [MW5 Merc Save Editor](https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466).
  No code from it is used; this editor was written from scratch.
- 'Mech pictures are read at runtime from your own MechWarrior 5 install and are never bundled.
- The Rajdhani font is under the SIL OFL.
- Full notices, including data catalog licensing: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

This is an unofficial fan tool, not affiliated with Piranha Games.
