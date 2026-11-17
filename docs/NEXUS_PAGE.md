# NexusMods page: MW5 Save Editor

Everything needed to create the NexusMods listings and keep them updated through `/publish`.

**Pages (one mod, uploaded for each game):**
- MechWarrior 5: Mercenaries: https://www.nexusmods.com/mechwarrior5mercenaries/mods/1666
- MechWarrior 5: Clans: https://www.nexusmods.com/mechwarrior5clans/mods/70

## Descriptions to paste (one per page)

- **Mercenaries page:** [nexus/description-mercenaries.bbcode](nexus/description-mercenaries.bbcode)
- **Clans page:** [nexus/description-clans.bbcode](nexus/description-clans.bbcode)

The descriptions are text only, with no screenshots or other images (removed for the 3.0.0 quarantine review,
M5SE-T124). Paste the whole file with the description editor's **Import description** (BBCode). NexusMods' API can't edit descriptions or upload mod images, so
this step is manual. The single-description version further down is kept for reference.

## One-time setup: create the page (manual; NexusMods has no API for this)

1. On nexusmods.com, open **MechWarrior 5: Mercenaries**, choose **Upload a mod**, and fill the form from
   the fields below. Paste the full description with the description editor's **Import description**
   (BBCode).
2. **Images:** the square icon (512×512) is the primary image, and the header (1920×1080) goes in the
   page's media settings. No screenshots: the description and the media gallery stay without them.
3. **First file:** upload `dist/MechWarrior5.SaveEditor-<version>.zip` (from `tools/package.ps1`) as a *Main file*.
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
- [ ] **Clans:** a save with a mech removed, and one with a duplicated mech, load from the menu and the
  mech bay plans show right (M5SE-T77).

## Mod name

MW5 Save Editor - Mercenaries and Clans

## Version

Set by `/publish`: whole numbers only, each release bumps the major (1.0.0, then 2.0.0, ...).

## Category

Utilities

## Summary (max 350 characters)

The complete save editor for MechWarrior 5: Mercenaries and Clans. Add any 'Mech from every DLC with factory loadouts, refit in a hardpoint-aware Mech Lab, repair and duplicate in bulk, and set C-Bills, Kerenskies, Honor, salvage, pilots, factions and inventory. Reads your mods. Saves stay safe: every edit becomes a new, named save in the menu.

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

[b]Mech Lab: hardpoint-aware refits (Mercenaries)[/b]

[b]Clans resources: Kerenskies, Honor, salvage, crew, MXP[/b]

[b]Clans mechs: repair, duplicate, rename[/b]

[b]Pick saves by name, not by GUID[/b]

[b]Two themes: Mercs amber and Clans ice[/b]

[size=5][b]Installation instructions[/b][/size]
[b]This is a program you run, not a mod the game loads.[/b] The download is a folder: [b]MW5SaveEditor.exe[/b], the .NET runtime files it needs, and an [b]Addons[/b] folder; keep them together. The game never starts it and it adds nothing to the game: you run it yourself. It never connects to the internet.

[list=1]
[*]Click [b]Manual Download[/b] on the main file (the same zip is mirrored on [url=https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/releases/latest]GitHub[/url]) and extract it anywhere, for example your Desktop.
[*]Double-click [b]MW5SaveEditor.exe[/b].
[/list]

[b]Using it[/b]
[list=1]
[*]Quit the game, or stay on its main menu, so the save you edit isn't in use.
[*]Run [b]MW5SaveEditor.exe[/b] and choose [b]MERCENARIES[/b] or [b]CLANS[/b] at the top. Your saves are listed by name; double-click one to open it.
[*]Make your changes, type a name for the edited save, and press [b]Save as new[/b] (Ctrl+S).
[*]In the game, load the save with that name. Your original save is still there, unchanged.
[/list]

[b]Uninstalling:[/b] delete the folder you extracted. Your settings are the [b]settings.json[/b] beside the exe; the editor writes nothing anywhere else except your saves.

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
[*][b]Every 'Mech variant[/b] through [b]DLC 9: Ashes & Ascension[/b], each with its picture from your own game files, tonnage, weight class, hero name, DLC source and how many you own. Modded 'Mechs from your enabled mods are listed too.
[*][b]A checkbox on every row:[/b] Shift+click ticks a range, Ctrl+A or [b]Select all shown[/b] takes everything the filters show.
[*][b]Add N to cold storage[/b] in one click, with copies of each. Every 'Mech arrives with its factory loadout and default paint. [b]One summary per batch[/b], not a message box per 'Mech.
[*][b]Mech Lab:[/b] each hardpoint offers only the weapons that fit it, at every tier; equipment slots, front and rear armor against the maximum, mech traits, and .mw5loadout export and import. Applying a refit changes only what you refitted; a damaged 'Mech keeps its damage until you repair it.
[*][b]Hangar:[/b] repair, reset to stock or remove cold-storage 'Mechs, in batches.
[*][b]Company:[/b] C-Bills (+1M / +10M / +100M / Max), reputation, name, and the campaign date (payroll and contracts move with it). Export or import a whole company. Cantina career paths and Arena fame, read from the game's own data.
[*][b]Inventory:[/b] any weapon, equipment or ammo at any tier, in bulk, optionally with each weapon's ammo (paired by the game's ammo types).
[*][b]Pilots:[/b] hire (like the selected pilot, as a rookie, or with random skills), release, persona, portrait, voice, bio, traits, skills and XP.
[*][b]Sarna.net links:[/b] every 'Mech has a link that opens the 'Mech's Sarna.net page in your browser.
[*][b]Factions:[/b] standing from -100 to +100, or set all allied or neutral.
[*]DLC and mod aware: content the campaign or game can't load is flagged before you save.
[*][b]Follows your mods:[/b] every enabled mod (Mods folder and Steam Workshop) is read from its own files: items, traits, 'Mechs, pictures and the pilot skill curve (Pilot Overhaul's caps of 30 included).
[*][b]Add-on packs:[/b] JSON files in the Addons folder carry a mod's values and apply only while that mod is installed, enabled and a supported version.
[*][b]Compatibility report:[/b] everything in a save that isn't vanilla, with the mod, version and page behind it, or why that mod isn't active.
[*]Six languages: English, Deutsch, Español, Français, 日本語, Русский.
[/list]

[b]MechWarrior 5: Clans[/b]
[list]
[*][b]Resources:[/b] Kerenskies, Merits and Honor (Honor moves the Honor level track too), mech, weapon and equipment components, technicians (and the mech bay's technician pool), scientists, crew upgrades, and per-chassis mech experience.
[*][b]Mechs:[/b] pictures from your Clans files (with an Oodle DLL already on your PC) or an installed MechWarrior 5: Mercenaries; repair armor and every installed part, duplicate with the same loadout, rename, or remove; the mech bay plan of each 'Mech follows it. 'Mechs deployed in your Star or in the work queue are never removed.
[*][b]Weapons and equipment:[/b] edit stock, requisition any item seen in Clans campaigns, or add to ticked items in bulk.
[*][b]Progress:[/b] Honor level (from the game's own Honor ladder), cycles, technician pool, the research queue and completed research.
[*][b]Compatibility report:[/b] assets a save uses that the base game lacks, with the installed Clans mod behind each (on or off).
[/list]

[b]Mod support[/b]
[list]
[*]Every installed mod is read from its own files at startup, with no per-mod code; a new or updated mod that uses the game's own data is followed without an editor update.
[*]If one mod misbehaves, a small add-on pack (JSON data) fixes or tweaks it; report it on [url=https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/issues]GitHub[/url] with the mod's name and version.
[*]Limits: mods that change rules only in their code can't be read; Clans mods are recognized by name and traced in the compatibility report, their detailed stats aren't read yet; chain fire, the quest log and moving 'Mechs in or out of the active bay aren't offered.
[/list]

[b]Built to feel like MW5[/b]
[list]
[*]A dark gunmetal and amber [b]Mercs[/b] look for Mercenaries and a cold steel and ice-cyan [b]Clans[/b] look for Clans, modeled on the games' own menus; the look follows the game tab.
[*]Saves listed by their in-game names, dates and money instead of GUID file names.
[/list]

[size=5][b]Requirements[/b][/size]
[list]
[*]MechWarrior 5: Mercenaries and/or Clans on Windows (Steam or Epic). Game Pass / Microsoft Store saves live elsewhere and are untested; use Open to pick them at your own risk.
[*]Windows 10 or 11, 64-bit. The download is self-contained: no .NET install needed.
[*]No DLC or other mods required. Back up a campaign you care about before editing it, as with any save editor.
[*]No internet access: the program never connects to the internet.
[/list]

[size=5][b]Shout outs[/b][/size]
[list]
[*][b]JJ[/b], whose [url=https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466]MW5 Merc Save Editor[/url] inspired this one's scope. No code from it is used; its game data catalogs (MIT) seed the Mercenaries 'Mech and item lists.
[*][b]wmtorode[/b], whose original MW5 Mercs Save Editor showed that editing these saves was possible in the first place.
[*]The authors of the mods the bundled add-on packs describe; each pack credits and links its mod.
[*][b]Piranha Games[/b] for MechWarrior 5: Mercenaries and Clans.
[*]Code written with an AI model (Claude).
[/list]

Made by MindAttic. Source: [url=https://github.com/mindattic/Mods.MechWarrior5.SaveEditor]github.com/mindattic/Mods.MechWarrior5.SaveEditor[/url]. MechWarrior and BattleTech are trademarks of their respective owners. This is an unofficial fan tool, not affiliated with Piranha Games.
```
