# MW5 Save Editor: user guide

A save editor for **MechWarrior 5: Mercenaries** and **MechWarrior 5: Clans**. This guide also ships
in the download as `README.md`.

## Install

**This is a program you run, not a mod the game loads.** The game never starts it and it adds nothing
to the game, so it isn't installed through NexusMods or Vortex.

1. Download `MechWarrior5.SaveEditor-<version>.zip` from
   [GitHub](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/releases/latest). The file on
   NexusMods is just a page with that link.
2. Extract it anywhere, for example your Desktop.
3. Double-click **MW5SaveEditor.exe**. No .NET install is needed.

To uninstall, delete the folder. Your settings are the `settings.json` beside the exe. The mech
picture cache lives in `%LOCALAPPDATA%\MechWarrior5SaveEditor`; delete that too for a clean removal.

## Before you edit

- Quit the game, or stay on its main menu, so the save isn't in use.
- Your original saves are never changed (see [Safety](#safety)). It is still wise to keep a copy of a
  campaign you care about, as with any save editor.

## The window

- **MERCENARIES / CLANS** (title bar, left) chooses the game. Each keeps its own open save, and the
  color theme follows it: amber for Mercenaries, ice blue for Clans.
- The **left rail** shows the open save, the pages, the **Save name** box and the save buttons.
- The **status bar** at the bottom confirms what each action did.

Keyboard: **Ctrl+O** open, **Ctrl+S** save, **Ctrl+Shift+S** save as (Mercenaries). They work anywhere in
the window, and Ctrl+S includes a number or name you're still typing. In any list, **Shift+click**
ticks a range and **Ctrl+A** ticks everything shown. A search or filter unticks the rows it hides, and
batch buttons stay greyed out until something is ticked, so an action only ever touches rows you can see.

## Saving

1. Type a name in **Save name (load menu)**. This is how the save appears in the game's load menu.
   For an original game save it starts as "*original name* [Edited]".
2. Press **Save as new** (for an original game save) or **Save** (for a save the editor made).
3. In the game, load the save with that name. The original is still listed, unchanged.

## MechWarrior 5: Mercenaries

Saves are in `%LOCALAPPDATA%\MW5Mercs\Saved\SaveGames\<campaign>`. The start page lists your
campaigns and their saves by name; double-click one to open it, or use **Open** for any `.sav` file.

- **Company.**
  - C-Bills, with +1M, +10M, +100M and Max.
  - Reputation, company name, and the campaign date. Moving the date moves payroll, injuries and
    contracts with it, so their timing is kept.
  - Which DLC the campaign has.
  - **Export company** writes mechs, pilots and inventory to a `.mw5company` file. **Import** adds them
    to another campaign: mechs arrive in cold storage fully repaired, and injured pilots arrive healthy.
  - Mech trait unlocks and locked Cantinas.
- **Mechs.**
  - The left list is your hangar (MechBay and cold storage): repair, reset to the stock loadout, or
    remove cold-storage mechs.
  - Double-click a mech, or **Edit loadout**, to open the **Mech Lab**: weapons per hardpoint (only
    what fits), equipment per slot, front and rear armor against the maximum, mech traits, and
    `.mw5loadout` export and import. **Apply** changes only what you refitted: a damaged mech keeps its
    missing armor and damaged parts (use **Repair** to fix them).
  - The right list is **requisition**: every variant, filterable by search, weight class, heroes, DLC
    pack, or *owned DLC only*. Tick any number and **Add to cold storage**, with copies of each if you
    like. They arrive with factory loadouts and paint.
- **Inventory.** Add any weapon, equipment or ammo at any tier in bulk, set counts for ticked stacks,
  and remove stacks.
- **Pilots.** Hire and release pilots. Change name, callsign, persona, portrait, voice, bio and
  traits. Edit skill caps and XP; heal, or max all caps.
- **Factions.** Standing from -100 to +100 for every faction, or set all allied or neutral.
- **Settings.**
  - Language (English, Deutsch, Español, Français, 日本語, Русский).
  - The game folder (found automatically on Steam and Epic), so installed DLC and enabled mods are
    recognized.
  - Owned-DLC filtering and mech pictures.

If you add something the installed game or its enabled mods don't have, the editor warns you before
saving.

## MechWarrior 5: Clans

Saves are in `%LOCALAPPDATA%\MW5Clans\Saved\SaveGames`. The start page lists every save by its
load-menu name, with campaign, date and Kerenskies. Autosaves are marked **AUTO**.

- **Resources.**
  - Kerenskies, Merits and Honor. Changing Honor also moves the Honor level track.
  - Salvage: mech, weapon and equipment components.
  - Crew: technicians (this also sets the mech bay's technician pool), scientists, and the three crew
    upgrades.
  - Mech experience (MXP) for each chassis you've earned it on.
  - Each value has quick +10, +100 and +1K buttons.
- **Mechs.** Every mech with its picture, variant, weight, installed gear and condition (READY or
  DAMAGED). Tick mechs (or **Select damaged**), then:
  - **Repair:** restores armor and every installed part to working order.
  - **Duplicate:** adds copies with the same loadout, fully repaired (up to 20 of each at a time), each
    with its own mech bay plan.
  - **Rename:** sets a custom name, on the mech and its mech bay plan; leave the box empty to clear it.
  - **Remove:** deletes ticked mechs along with their mech bay plans. Mechs that are deployed in your
    Star, or in the mech bay's work queue, are kept and listed in the summary.
- **Inventory.** Your weapons and equipment, with editable counts. **Requisition** adds any weapon or
  piece of equipment seen in Clans campaigns. **+1 / +5 / +10 each** and **Remove** act on ticked items.
- **Progress.** Honor level and experience, cycles elapsed, the technician pool, the research queue
  and completed research.
- **Settings.** The save folder, and mech pictures.

## Safety

- **Original saves are never overwritten.** Saving an original game save always creates a new save
  file and lists it in the load menu under your name. The original stays byte-for-byte identical.
- **Editor saves are marked.** Saves the editor writes carry a tiny marker the game ignores. Only
  those are updated in place, and the previous version is kept as a `.bak` file next to it.
- **Unreadable saves are refused.** Every save must read back exactly before editing is allowed. If
  one doesn't, the editor refuses to edit it rather than guess.
- **Index backups.** The game's save index files (`Campaign.json`, `UserSaveMetaData*.sav`) are backed
  up to `.bak` before every change.

## Troubleshooting

| Problem | What to do |
| --- | --- |
| My edited save isn't in the load menu | Check the status bar after saving. If it says the save wasn't found in the index, open the folder and use the new file name, or re-save from an original. Restart the game so it rereads its index. |
| "Can't open save" | The file isn't a campaign save, or it doesn't read back exactly. The editor refuses rather than risk it; please report the game version. |
| Mech pictures are missing | They download from Sarna.net on first view. Check your connection, or turn them off in Settings. |
| A batch button is greyed out | Tick at least one row first. Rows hidden by a search or filter are unticked. |
| The NexusMods download has no program in it | That file is only a link: download the editor from [GitHub](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/releases/latest) (see [Install](#install)). |
| I want my original back | It was never changed. Load it from the game's menu. Editor saves keep their previous version as `.bak`. |

## What's new in 2.0.0

- A rebuilt interface: one HUD-style look across every page, dialog and the Mech Lab, a linked
  MERCENARIES / CLANS game picker, and the look follows the game.
- Batch buttons stay greyed out until something is ticked; searches and filters untick what they hide.
- Mercenaries: Mech Lab refits keep a mech's damage; imported mechs arrive repaired and pilots healthy.
- Clans: Remove mechs works, mech bay plans follow their mechs, and Repair restores destroyed parts.
- Safer saving: files are written to a temporary file first, and locked files no longer break the save list.
- Everything translated in all six languages.

The full list is in the [changelog](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor#changelog).

## Credits

Inspired by JJ's MW5 Merc Save Editor (no code shared). Mech art comes from Sarna.net; the font is
Rajdhani (SIL OFL). This is an unofficial fan tool, not affiliated with Piranha Games.
