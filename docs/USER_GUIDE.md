# MW5 Save Editor: user guide

A save editor for **MechWarrior 5: Mercenaries** and **MechWarrior 5: Clans**. This guide also ships
in the download as `README.md`.

## Install

**This is a program you run, not a mod the game loads.** The download is a single
**MW5SaveEditor.exe** (no .NET install needed). The game never starts it and it adds nothing to the
game, so nothing changes until you run it yourself.

### With Vortex

1. Click **Mod Manager Download** on the NexusMods page. Vortex downloads and installs it like any
   mod. The game ignores it.
2. Vortex doesn't start it for you. In Vortex's **Mods** list, right-click **MW5 Save Editor** and
   choose **Open in File Manager**.
3. In the folder that opens, double-click **MW5SaveEditor.exe**.

To start it from Vortex next time, add it as a tool: on Vortex's **Dashboard**, open the **Tools**
panel, choose **Add Tool**, and set its target to that same **MW5SaveEditor.exe** (Vortex fills in
the rest). Or right-click the exe and send a shortcut to your Desktop.

### Manual

1. Click **Manual Download** and extract the archive anywhere, for example your Desktop.
2. Double-click **MW5SaveEditor.exe**.

### Uninstall

Remove it in Vortex, or delete the folder you extracted. Your settings are the `settings.json` beside
the exe. The mech picture cache lives in `%LOCALAPPDATA%\MechWarrior5SaveEditor`; delete that too for
a clean removal.

## Before you edit

- Quit the game, or stay on its main menu, so the save isn't in use.
- Your original saves are never changed (see [Safety](#safety)). It is still wise to keep a copy of a
  campaign you care about, as with any save editor.

## The window

- **MERCENARIES / CLANS** (title bar, left) chooses the game. Each keeps its own open save.
- **MERCS / CLANS** (title bar, right) chooses the color theme. It is independent of the game.
- The **left rail** shows the open save, the pages, the **Save name** box and the save buttons.
- The **status bar** at the bottom confirms what each action did.

Keyboard: **Ctrl+O** open, **Ctrl+S** save, **Ctrl+Shift+S** save as (Mercenaries). In any list,
**Shift+click** ticks a range and **Ctrl+A** ticks everything shown.

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
    to another campaign.
  - Mech trait unlocks and locked Cantinas.
- **Mechs.**
  - The left list is your hangar (MechBay and cold storage): repair, reset to the stock loadout, or
    remove cold-storage mechs.
  - Double-click a mech, or **Edit loadout**, to open the **Mech Lab**: weapons per hardpoint (only
    what fits), equipment per slot, front and rear armor against the maximum, mech traits, and
    `.mw5loadout` export and import.
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
  - **Duplicate:** adds copies with the same loadout, fully repaired.
  - **Rename:** sets a custom name; leave the box empty to clear it.
  - **Remove:** deletes ticked mechs. Mechs that are deployed in your Star, or in the mech bay's work
    queue, are kept and listed in the summary.
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
| I want my original back | It was never changed. Load it from the game's menu. Editor saves keep their previous version as `.bak`. |

## Credits

Inspired by JJ's MW5 Merc Save Editor (no code shared). Mech art comes from Sarna.net; the font is
Rajdhani (SIL OFL). This is an unofficial fan tool, not affiliated with Piranha Games.
