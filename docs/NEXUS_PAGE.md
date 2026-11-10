# Nexus Mods page: MW5 Mercs Save Editor

Not published yet (live link goes here once uploaded). Copy each field into the Nexus Mods upload form.
The full description is BBCode: paste it with **Import description** in the editor's top right corner.

## Before uploading

Developed against real campaign saves (23 of them, 2022-2026, game v1.15): every one round-trips
byte-for-byte, and added mechs, loadouts and edits survive save and reload in the editor. Not yet loaded
in game after editing; check at least:

- [ ] A save written by the editor appears in MW5's load menu under the name typed in **Save name**, and
      the original save is still listed and unchanged.
- [ ] It loads; C-Bills, reputation, faction standings and pilot skills show the edited values.
- [ ] 'Mechs added to cold storage show in Cold Storage with their stock loadouts and correct paint, and
      can be moved to the MechBay and dropped into a mission.
- [ ] A loadout changed in the Mech Lab shows in the in-game MechLab and fires in a mission.
- [ ] A DLC 9 (Ashes & Ascension) 'Mech added to a campaign that has DLC 9 works.

## Mod name

MW5 Mercs Save Editor

## Version

2.0.0 (upload the self-contained `MW5SaveEditor-2.0.0-win-x64.zip`, see M5SE-T10)

## Category

Utilities (alternative: Tools)

## Summary (max 350 characters)

The MechWarrior 5: Mercenaries save editor, rebuilt. Tick any number of 'Mechs (Shift+click, select all) and add them to cold storage with stock loadouts, refit them in a Mech Lab, and edit C-Bills, inventory, pilots and factions. Every DLC through Ashes & Ascension. Never overwrites your saves: edits go to a new, named save.

## Tags

Utilities, Save Games, Mechs, Cheats and God items, User Interface

## Requirements

- MechWarrior 5: Mercenaries (Steam or Epic; saves in `%LOCALAPPDATA%\MW5Mercs\Saved\SaveGames`).
- Windows 10 or 11 (64-bit). Self-contained: no .NET install needed.
- DLC: not required. DLC 'Mechs can be added only to campaigns that have that DLC.
- No other mods required.

## AI disclosure

The editor's code was written with an AI model (Claude). Tick Nexus's "contains AI-generated content"
option when uploading (and again if the header or icon art is AI-generated).

## Images

| File | Use |
|---|---|
| Square icon (512×512) | Primary image / mod card |
| Header (1920×1080) | Page header in the page's media settings |
| Screenshots | Campaign picker, Mechs page with ticked rows, Mech Lab, batch summary dialog |

Once the screenshots are uploaded, right-click each one on the mod page, copy its image address, and
paste it over `IMAGE_1_URL` to `IMAGE_4_URL` in the description below.

## Full description (BBCode)

```
[size=5][b]Description[/b][/size]
A save editor for [b]MechWarrior 5: Mercenaries[/b], rebuilt from scratch around three ideas: never risk your saves, let you do in one click what used to take fifty, and look like it belongs in the game.

It edits your campaign the way the game itself writes it. Every save is read into a complete, typed copy and written back byte-for-byte before any edit is allowed, so the editor never guesses at offsets. New 'Mechs arrive with their real factory loadouts, not a borrowed Griffin's.

[b]Requisition: tick, shift-click, add[/b]
[img]IMAGE_1_URL[/img]

[b]Mech Lab: hardpoint-aware refits[/b]
[img]IMAGE_2_URL[/img]

[b]One summary for the whole batch[/b]
[img]IMAGE_3_URL[/img]

[b]Pick saves by name, not by GUID[/b]
[img]IMAGE_4_URL[/img]

[size=5][b]Installation instructions[/b][/size]
[list=1]
[*]Download the archive and extract it anywhere, for example your Desktop. It is a standalone program, not a game mod: nothing goes into the game folder.
[*]Close MW5 to the main menu (or quit it) so the save you edit isn't in use.
[*]Run [b]MW5SaveEditor.exe[/b]. Your campaigns are listed by name; double-click a save to open it.
[*]Make your changes, type a name for the edited save, and press [b]Save as new[/b] (Ctrl+S).
[*]In MW5, load the save with that name. Your original save is still there, unchanged.
[/list]

[b]Uninstalling:[/b] delete the folder (your settings are the [b]settings.json[/b] inside it). The 'Mech picture cache lives in [b]%LOCALAPPDATA%\Mech5SaveEditor[/b]; delete it too for a clean removal.

[size=5][b]Main features[/b][/size]
[b]Your saves are safe.[/b]
[list]
[*]An original game save is [b]never overwritten[/b]. Saving writes a new save beside it and adds it to the game's load menu under the name you choose (default: "<original name> [Edited]"). The original stays byte-for-byte identical.
[*]Saves the editor writes carry a small, harmless marker the game ignores. Only those are ever updated in place, and the previous version is kept as a [b].bak[/b].
[*][b]Lossless or nothing:[/b] if a save can't be reproduced exactly, the editor refuses to edit it instead of guessing.
[*]The campaign index (Campaign.json) is backed up before every change.
[/list]

[b]'Mech logistics[/b]
[list]
[*][b]All 639 'Mech variants[/b], every DLC through [b]DLC 9: Ashes & Ascension[/b], each with its Sarna.net artwork, tonnage, weight class, hero name, DLC source, and how many you already own.
[*][b]A checkbox on every row.[/b] Shift+click ticks a whole range, Ctrl+A or [b]Select all shown[/b] takes everything the filters show, and the header box toggles the visible set.
[*]Filter by search (code, chassis, hero, DLC), weight class, hero 'Mechs, DLC pack, or [b]owned DLC only[/b].
[*][b]Add N to cold storage[/b] in one click, with copies of each if you like. Every 'Mech arrives with its factory loadout (armor, structure, weapons, weapon groups, equipment) and its default paint.
[*][b]One summary per batch:[/b] what was added and what was skipped and why, in a single dialog instead of a message box per 'Mech.
[*]DLC aware: 'Mechs from DLC your campaign doesn't have are flagged and skipped, so a save can't be broken by content the game can't load.
[*]The company hangar lists your MechBay and cold-storage 'Mechs: [b]repair[/b], [b]reset to stock loadout[/b] or remove cold-storage 'Mechs, in batches or all at once.
[/list]

[b]Mech Lab[/b]
[list]
[*]Double-click any hangar 'Mech to refit it. Each hardpoint offers only the weapons that fit it (energy, ballistic, missile, AMS, omni, melee), at every tier.
[*]Equipment slots offer only what fits them (general, engine heat sinks, jump jets by class, ECM, probes, MASC).
[*]Front and rear armor per location against the variant's maximum, plus [b]Max armor[/b]. Weapon groups are kept.
[*][b]Export and import loadouts[/b] as .mw5loadout files to reuse or share builds.
[/list]

[b]Company, inventory, pilots, factions[/b]
[list]
[*][b]Company:[/b] C-Bills (with +1M / +10M / +100M / Max), reputation, company name, campaign date and the campaign's DLC status.
[*][b]Inventory:[/b] add any weapon, equipment or ammo at any tier in bulk, set counts for many stacks at once, remove stacks.
[*][b]Pilots:[/b] skill caps, XP per skill, salary, heal; heal all and max all caps.
[*][b]Factions:[/b] standing meters from -100 to +100 for every faction, or set all allied or neutral at once.
[/list]

[b]Built to feel like MW5[/b]
[list]
[*]A dark gunmetal and amber interface modeled on the game's own menus (cut-corner panels, condensed type, a tactical grid), plus a cold steel and ice-cyan [b]Clans[/b] theme. Switch any time; the choice is remembered.
[*]Your campaigns and saves listed by their in-game names, dates, systems and reputation instead of GUID file names. Saves the editor made are marked [b]EDITED[/b].
[/list]

[size=5][b]Requirements[/b][/size]
[list]
[*]MechWarrior 5: Mercenaries on Windows (Steam or Epic). Game Pass / Microsoft Store saves live elsewhere and are untested; use Browse to open them at your own risk.
[*]Windows 10 or 11, 64-bit. The download is self-contained: no .NET install needed.
[*]No DLC or other mods required. Back up a save you care about before editing it, as with any save editor.
[*]Internet access only for the 'Mech pictures (from Sarna.net, cached after the first view). Everything else works offline.
[/list]

[size=5][b]Shout outs[/b][/size]
[list]
[*][b]JJ[/b], whose [url=https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466]MW5 Merc Save Editor[/url] inspired this one's scope: stock loadouts, repairs and bulk editing. No code from it is used; its game data catalogs (MIT) seed the 'Mech and item lists.
[*][b]wmtorode[/b], whose original MW5 Mercs Save Editor showed that editing these saves was possible in the first place.
[*][url=https://www.sarna.net]Sarna.net[/url], the BattleTech wiki, for the 'Mech artwork shown in the editor.
[*][b]Piranha Games[/b] for MechWarrior 5: Mercenaries and nine DLCs' worth of 'Mechs.
[*]Code written with an AI model (Claude).
[/list]

Made by MindAttic. Source: [url=https://github.com/mindattic/Mods.Mech5.Mercs.SaveEditor]github.com/mindattic/Mods.Mech5.Mercs.SaveEditor[/url]. MechWarrior and BattleTech are trademarks of their respective owners. This is an unofficial fan tool, not affiliated with Piranha Games.
```
