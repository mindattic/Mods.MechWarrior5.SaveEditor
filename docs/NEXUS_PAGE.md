# NexusMods page: MW5 Save Editor

Everything needed to create the NexusMods listing and keep it updated through `/publish`.

## One-time setup: create the page (manual; NexusMods has no API for this)

1. On nexusmods.com, open **MechWarrior 5: Mercenaries**, choose **Upload a mod**, and fill the form from
   the fields below. Paste the full description with the description editor's **Import description**
   (BBCode).
2. **Images:** the square icon (512×512) is the primary image, and the header (1920×1080) goes in the
   page's media settings. Add the screenshots listed in [SCREENSHOTS.md](SCREENSHOTS.md), then paste each
   screenshot's image address over `IMAGE_1_URL` to `IMAGE_6_URL` in the description.
3. **First file:** upload `dist/MW5SaveEditor-<version>.zip` (from `tools/package.ps1`) as a *Main file*.
4. **Wire up `/publish`:** put the page's mod ID (from its URL, `.../mods/<modId>`) and the file's ID
   (Manage files, then the file's edit menu, then API info) into `nexus.json` as `modId` and `fileId`. From then on,
   `/publish` uploads every new version to that file slot.
5. Optional: create a second page on **MechWarrior 5: Clans** with the same text and link it, so Clans
   players find it too. The download is the same file.

## Before the first upload (in-game checks)

Every Mercs campaign save tested (23 saves) and every Clans save tested (24 saves plus 4 indexes)
round-trips byte-for-byte, and edits survive save and reload in the editor. Before the first upload,
check in game:

- [ ] **Mercs:** an edited save appears in the load menu under its new name, the original is still
      listed, and it loads with the edited C-Bills, mechs, pilots and factions.
- [ ] **Mercs:** added cold-storage mechs have stock loadouts and paint, can move to the MechBay, and
      drop into a mission.
- [ ] **Clans:** an edited save appears in the load menu under its new name and loads with the edited
      Kerenskies, Merits, Honor, salvage and crew.
- [ ] **Clans:** repaired and duplicated mechs show in the mech bay and can be deployed.

## Mod name

MW5 Save Editor - Mercenaries and Clans

## Version

Set by `/publish` (the first publish makes it 1.0.0).

## Category

Utilities

## Summary (max 350 characters)

One save editor for MechWarrior 5: Mercenaries and Clans. Tick any number of 'Mechs and add, repair, duplicate or refit them; edit C-Bills, Kerenskies, Honor, salvage, crew, inventory, pilots and factions. Every DLC through Ashes & Ascension. HUD-styled UI. Never overwrites your saves: edits go to a new, named save.

## Tags

Utilities, Save Games, Mechs, Cheats and God items, User Interface

## Requirements

- MechWarrior 5: Mercenaries and/or MechWarrior 5: Clans on Windows (Steam or Epic).
- Windows 10 or 11 (64-bit). Self-contained: no .NET install needed.
- No DLC or other mods required. Mercs DLC 'Mechs can be added only to campaigns that have that DLC.

## AI disclosure

The editor's code was written with an AI model (Claude). Tick NexusMods' "contains AI-generated
content" option when uploading (and again if the header or icon art is AI-generated).

## Full description (BBCode)

```
[size=5][b]Description[/b][/size]
One save editor for [b]MechWarrior 5: Mercenaries[/b] and [b]MechWarrior 5: Clans[/b], built from scratch around three ideas: never risk your saves, do in one click what used to take fifty, and look like it belongs in the game.

Switch games with the tabs at the top; each keeps its own open save. Every save is read into a complete copy and written back byte-for-byte before any edit is allowed, so the editor never guesses at offsets.

[b]Requisition: tick, shift-click, add[/b]
[img]IMAGE_1_URL[/img]

[b]Mech Lab: hardpoint-aware refits (Mercenaries)[/b]
[img]IMAGE_2_URL[/img]

[b]Clans resources: Kerenskies, Honor, salvage, crew, MXP[/b]
[img]IMAGE_3_URL[/img]

[b]Clans mechs: repair, duplicate, rename[/b]
[img]IMAGE_4_URL[/img]

[b]Pick saves by name, not by GUID[/b]
[img]IMAGE_5_URL[/img]

[b]Two themes: Mercs amber and Clans ice[/b]
[img]IMAGE_6_URL[/img]

[size=5][b]Installation instructions[/b][/size]
[list=1]
[*]Download the archive and extract it anywhere, for example your Desktop. It is a standalone program, not a game mod: nothing goes into the game folder.
[*]Quit the game, or stay on its main menu, so the save you edit isn't in use.
[*]Run [b]MW5SaveEditor.exe[/b] and choose [b]MERCENARIES[/b] or [b]CLANS[/b] at the top. Your saves are listed by name; double-click one to open it.
[*]Make your changes, type a name for the edited save, and press [b]Save as new[/b] (Ctrl+S).
[*]In the game, load the save with that name. Your original save is still there, unchanged.
[/list]

[b]Uninstalling:[/b] delete the folder (your settings are the [b]settings.json[/b] inside it). The 'Mech picture cache lives in [b]%LOCALAPPDATA%\MechWarrior5SaveEditor[/b]; delete it too for a clean removal.

[size=5][b]Main features[/b][/size]
[b]Your saves are safe.[/b]
[list]
[*]An original game save is [b]never overwritten[/b]. Saving writes a new save beside it and adds it to the game's load menu under the name you choose. The original stays byte-for-byte identical.
[*]Saves the editor writes carry a small, harmless marker the game ignores. Only those are ever updated in place, and the previous version is kept as a [b].bak[/b].
[*][b]Lossless or nothing:[/b] if a save can't be reproduced exactly, the editor refuses to edit it instead of guessing.
[*]The game's save index files are backed up before every change.
[/list]

[b]MechWarrior 5: Mercenaries[/b]
[list]
[*][b]Every 'Mech variant[/b] through [b]DLC 9: Ashes & Ascension[/b], each with its Sarna.net artwork, tonnage, weight class, hero name, DLC source and how many you own. Modded 'Mechs from your enabled mods are listed too.
[*][b]A checkbox on every row:[/b] Shift+click ticks a range, Ctrl+A or [b]Select all shown[/b] takes everything the filters show.
[*][b]Add N to cold storage[/b] in one click, with copies of each. Every 'Mech arrives with its factory loadout and default paint. [b]One summary per batch[/b], not a message box per 'Mech.
[*][b]Mech Lab:[/b] each hardpoint offers only the weapons that fit it, at every tier; equipment slots, front and rear armor against the maximum, mech traits, and .mw5loadout export and import.
[*][b]Hangar:[/b] repair, reset to stock or remove cold-storage 'Mechs, in batches.
[*][b]Company:[/b] C-Bills (+1M / +10M / +100M / Max), reputation, name, and the campaign date (payroll and contracts move with it). Export or import a whole company.
[*][b]Inventory:[/b] any weapon, equipment or ammo at any tier, in bulk.
[*][b]Pilots:[/b] hire, release, persona, portrait, voice, bio, traits, skills and XP.
[*][b]Factions:[/b] standing from -100 to +100, or set all allied or neutral.
[*]DLC and mod aware: content the campaign or game can't load is flagged before you save.
[*]Six languages: English, Deutsch, Español, Français, 日本語, Русский.
[/list]

[b]MechWarrior 5: Clans[/b]
[list]
[*][b]Resources:[/b] Kerenskies, Merits and Honor (Honor moves the Honor level track too), mech, weapon and equipment components, technicians (and the mech bay's technician pool), scientists, crew upgrades, and per-chassis mech experience.
[*][b]Mechs:[/b] repair armor and every installed part, duplicate with the same loadout, rename, or remove. 'Mechs deployed in your Star or in the work queue are never removed.
[*][b]Weapons and equipment:[/b] edit stock, requisition any item seen in Clans campaigns, or add to ticked items in bulk.
[*][b]Progress:[/b] Honor level, cycles, technician pool, the research queue and completed research.
[/list]

[b]Built to feel like MW5[/b]
[list]
[*]A dark gunmetal and amber [b]Mercs[/b] theme and a cold steel and ice-cyan [b]Clans[/b] theme, modeled on the games' own menus. Switch any time; the choice is remembered.
[*]Saves listed by their in-game names, dates and money instead of GUID file names.
[/list]

[size=5][b]Requirements[/b][/size]
[list]
[*]MechWarrior 5: Mercenaries and/or Clans on Windows (Steam or Epic). Game Pass / Microsoft Store saves live elsewhere and are untested; use Open to pick them at your own risk.
[*]Windows 10 or 11, 64-bit. The download is self-contained: no .NET install needed.
[*]No DLC or other mods required. Back up a campaign you care about before editing it, as with any save editor.
[*]Internet access only for the 'Mech pictures (from Sarna.net, cached after the first view). Everything else works offline.
[/list]

[size=5][b]Shout outs[/b][/size]
[list]
[*][b]JJ[/b], whose [url=https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466]MW5 Merc Save Editor[/url] inspired this one's scope. No code from it is used; its game data catalogs (MIT) seed the Mercenaries 'Mech and item lists.
[*][b]wmtorode[/b], whose original MW5 Mercs Save Editor showed that editing these saves was possible in the first place.
[*][url=https://www.sarna.net]Sarna.net[/url], the BattleTech wiki, for the 'Mech artwork shown in the editor.
[*][b]Piranha Games[/b] for MechWarrior 5: Mercenaries and Clans.
[*]Code written with an AI model (Claude).
[/list]

Made by MindAttic. Source: [url=https://github.com/mindattic/Mods.MechWarrior5.SaveEditor]github.com/mindattic/Mods.MechWarrior5.SaveEditor[/url]. MechWarrior and BattleTech are trademarks of their respective owners. This is an unofficial fan tool, not affiliated with Piranha Games.
```
