# MW5 Save Editor: user guide

A save editor for **MechWarrior 5: Mercenaries** and **MechWarrior 5: Clans**. This guide also ships
in the download as `README.md`.

## Install

**This is a program you run, not a mod the game loads.** The game never starts it and it adds nothing
to the game, so it doesn't need Vortex. It never connects to the internet.

1. Download `MechWarrior5.SaveEditor-<version>.zip`: the main file on the NexusMods page
   ([Mercenaries](https://www.nexusmods.com/mechwarrior5mercenaries/mods/1677),
   [Clans](https://www.nexusmods.com/mechwarrior5clans/mods/70)), or the same zip from
   [GitHub](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/releases/latest).
2. Extract it anywhere, for example your Desktop. Keep the `Addons` folder beside the exe.
3. Double-click **MW5SaveEditor.exe**. The folder also holds the .NET runtime files it needs, so no .NET install is
   needed; keep them together.

To uninstall, delete the folder. Your settings are the `settings.json` beside the exe; the editor
writes nothing anywhere else except your saves.

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
  - **Career paths:** each Cantina career path's points, the level reached and the next level's 'Mech trait
    reward, with **Next level**, **Max** and **Max all**. Paths and levels come from the game's (and your
    mods') data. Rewards for reached levels are offered in the Cantina by the game, as usual. Until you've
    done a Cantina job in the game, paths not started yet can't be added.
  - **Arena fame** (DLC 6): fame points and the rank they reach, with **Next rank** and **Max**. Ranks
    come from the game's fame table; the game offers each newly reached rank's rewards itself.
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
  and remove stacks. Tick **+ ammo** to add each picked weapon's ammo too (that many tons each), paired by
  the game's own ammo types, mods included.
- **Pilots.** Hire and release pilots. **Hire as** picks the recruit's skills: like the selected pilot, a
  rookie with no experience, or random skills within your roster's range and the skill cap. Change name, callsign, persona, portrait, voice, bio and
  traits. Edit skill caps and XP; heal, or max all caps.
- **Factions.** Standing from -100 to +100 for every faction, or set all allied or neutral.
- **Settings.**
  - Language (English, Deutsch, Español, Français, 日本語, Русский).
  - The game folder (found automatically on Steam and Epic), so installed DLC and enabled mods are
    recognized.
  - Add-on packs and the compatibility report (see [Mods and add-ons](#mods-and-add-ons) and
    [Compatibility](#compatibility)).

'Mech pictures are read from your installed game (base game, DLC and mods); nothing is downloaded. For a 'Mech's
full technical specs, click the link icon in the requisition list, or VIEW ON SARNA.NET under an enlarged picture:
its Sarna.net page opens in your browser.

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
- **Mechs.** Every mech with its picture (from an installed MechWarrior 5: Mercenaries, when there is one), variant, weight, installed gear and condition (READY or
  DAMAGED). Tick mechs (or **Select damaged**), then:
  - **Repair:** restores armor and every installed part to working order.
  - **Duplicate:** adds copies with the same loadout, fully repaired (up to 20 of each at a time), each
    with its own mech bay plan.
  - **Rename:** sets a custom name, on the mech and its mech bay plan; leave the box empty to clear it.
  - **Remove:** deletes ticked mechs along with their mech bay plans. Mechs that are deployed in your
    Star, or in the mech bay's work queue, are kept and listed in the summary.
- **Pilots.** A card for each pilot in your Star:
  - **Unspent XP:** the pilot XP the game's skill screen spends on new ranks, with +1K and +10K buttons.
  - **Skills:** each skill the pilot has, with a slider for its rank (1 to 5) and a bin to remove it.
    **Pick a skill to add** offers any of the game's 37 pilot skills, added at rank 1; **Max skills** sets
    every skill the pilot has to rank 5.
  - A pilot appears once the game has given them a pilot record, after they first earn XP.
- **Inventory.** Your weapons and equipment, with editable counts. **Requisition** adds any weapon or
  piece of equipment seen in Clans campaigns. **+1 / +5 / +10 each** and **Remove** act on ticked items.
- **Progress.** Honor level and experience, cycles elapsed, the technician pool, the research queue
  and completed research. Honor levels follow the game's own Honor ladder, read from its files when an
  Oodle DLL is on your PC (the built-in ladder otherwise).
- **Settings.** The save folder, where 'Mech pictures come from, the installed Clans mods (each on or
  off), and the **Compatibility** report: every asset the open save uses that the base game doesn't have,
  with the installed mod that ships it (on or off), its version and page, or "unknown mod" when no
  installed mod has it. Detailed stats from Clans mods aren't read yet.

## Mods and add-ons

This section is about MechWarrior 5: Mercenaries; for Clans mods, see the Clans **Settings** above.

**Read automatically.** The editor reads every enabled mod straight from its own files: mods in the
game's `Mods` folder and Steam Workshop mods, in the game's load order (`modlist.json`). From each it
takes weapons, equipment and ammo (with their in-game names, hardpoints and slots), pilot and 'Mech
traits, 'Mechs and their pictures, and the pilot skill curve. A mod item shows its mod's name in the
pickers. If a mod raises the skill cap (Pilot Overhaul - Eternal goes to 30), the Pilots page follows it.
Disabled mods are not offered, but the editor still knows what they contain (see [Compatibility](#compatibility)).
Use **Rescan game and mods** in Settings after changing mods.

**Add-on packs** are JSON files in the `Addons` folder beside the exe. A pack carries one mod's values:
limits, names, and lists of what the mod adds. It applies only while its mod is installed, enabled and
a version the pack lists. Packs for these mods come with the editor: Additional Vanilla Traits and
Academies, PilotOverhaul - Eternal, Yet Another Weapon - Complete Edition, the YASV Complete Edition
Patch, and Yet Another Mechlab. You can drop more into the folder; the format is in
[docs/ADDONS.md](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/blob/main/docs/ADDONS.md).

Settings, **Add-ons** lists every pack with its status:

| Status | Meaning |
| --- | --- |
| ACTIVE | The mod is installed, enabled and a supported version; the pack applies. |
| Mod not installed | The mod isn't in the Mods folder or Steam Workshop. |
| Mod disabled in the game | The mod is installed but turned off in the game's mod list. |
| Mod version X not supported | The installed version isn't one the pack lists. |
| Needs the "X" add-on | The pack builds on another pack that isn't active. |
| Turned off | You switched it off with its checkbox. |
| Can't be read | The file isn't a valid pack; the reason is shown. |

Untick a pack to turn it off, or tick **Turn all add-ons off (vanilla)**. **Limits in force** shows each
limit (skill cap, experience, salary, C-Bills, faction standing, items per stack) and which mod or pack
set it. A value your save already has beyond a limit is kept as it is; the editor only stops you
pushing it further. Packs never write anything into a save.

## Compatibility

When you open a Mercenaries save, the editor checks it against the installed game and mods. Settings,
**Compatibility** lists everything in the save that differs from the vanilla game:

- **MODDED**: content or a value that comes from a mod that is active now. Expected.
- **NEEDS MOD**: content or a value that needs a mod that isn't active. The game may not load it.

Each line shows what it is (a 'Mech, item, trait, pilot skill, C-Bills...), how many times the save uses
it, the mod behind it with its version and page link, and why that mod doesn't cover it today: not
installed, installed but disabled in the game's mod list, the wrong version, or an add-on that is turned
off. "Unknown mod" means no installed mod or add-on pack contains it. If anything needs a mod, the
message after opening the save says so. The report only reads; it never changes the save.

## Reporting a mod problem

If a mod's content looks wrong in the editor (a weapon without its ammo, a wrong name, a limit that
doesn't match the mod), open an issue on
[GitHub](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/issues) with the mod's name and version
(Settings shows both) and, if it's about a save, the line from Settings, **Compatibility**. Most fixes are
an add-on pack: a small data file, no new editor needed. Mods that change rules only in their code can't be
read, and the editor won't guess.

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
| Mech pictures are missing | Pictures come from your installed MechWarrior 5: Mercenaries; check the game folder in Settings. Clans pictures come from the Clans files and need an Oodle DLL (`oo2core*.dll`) already on your PC: Clans Settings shows which one is used, or lets you choose one (an Unreal Engine install has one). Without it, Clans uses the Mercenaries picture of the same chassis. |
| A modded item or 'Mech is missing | Make sure the mod is enabled in the game's mod list, then press **Rescan game and mods** in Settings. |
| A save "needs a mod" | See Settings, **Compatibility**: it names the mod, its version and page. Install or enable it in the game, or leave that content as it is. |
| A batch button is greyed out | Tick at least one row first. Rows hidden by a search or filter are unticked. |
| I want my original back | It was never changed. Load it from the game's menu. Editor saves keep their previous version as `.bak`. |

## What's new in 6.0.0

- Clans: a new Pilots page. Set each pilot's unspent XP and choose their skills yourself: add any of the
  game's 37 pilot skills, set ranks 1 to 5, max them all, or remove one.

## What's new in 5.0.0

- Company: Cantina career paths and Arena fame, both read from the game's data.
- Inventory: add each weapon's ammo with it, paired by the game's ammo types.
- Pilots: hire as a rookie or with random skills.
- Clans: Honor levels from the game's own ladder; compatibility report and installed Clans mods in Settings.
- Every 'Mech links to its Sarna.net page (link icon in the requisition list, or VIEW ON SARNA.NET under an
  enlarged picture); it opens in your browser only when you click.
- The download is a folder: keep `MW5SaveEditor.exe` together with the .NET runtime files beside it.

## What's new in 3.0.0

- No network access at all: 'Mech pictures are read from your installed game instead of Sarna.net.
- Mods are read from their own files (Mods folder and Steam Workshop): their items, traits, 'Mechs,
  pictures and skill caps.
- Add-on packs in the `Addons` folder, with a status and switch for each in Settings.
- A compatibility report that names the mod, version and page behind anything in a save that isn't vanilla.

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

Inspired by JJ's MW5 Merc Save Editor (no code shared). 'Mech pictures are read from your own game
install; the add-on packs credit and link each mod's authors. The font is Rajdhani (SIL OFL). This is an unofficial fan tool, not affiliated with Piranha Games.
