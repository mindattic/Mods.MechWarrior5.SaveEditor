---
codex: 1
project: MW5-SaveEditor
code: M5SE
layer: stories
status: living
updated: 2026-11-09
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# MW5-SaveEditor — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| M5SE-T1 | 2026-11-09 | docs | closed | Start the permanent ticket log in the ticket database | — |
| M5SE-T2 | 2026-11-09 | feature | closed | Reverse-engineer MW5SaveEditor.exe v1.6.7 (save format, DLC1-9 mech lists) with REA and build our own editor: DLC9 support, mech multi-select (select all, shift-click ranges, row checkboxes), one post-add message per batch | — |
| M5SE-T3 | 2026-11-09 | feature | closed | Loadout editor: per-hardpoint weapon/equipment picker for hangar mechs, plus import/export of loadouts (parity with JJ's editor) | — |
| M5SE-T4 | 2026-11-09 | feature | closed | Pilot management: hire/remove pilots, personas (name, bio, portrait, voice), pilot traits and roster order | eebdab1 |
| M5SE-T5 | 2026-11-09 | feature | closed | Mech traits editor (experimental in JJ's editor) and Cantina career progress / upgrade unlock sync | a683b70 |
| M5SE-T6 | 2026-11-09 | feature | open | Mod support: scan the MW5 Mods folder's .pak files for mod-added mech variants and items and add them to the requisition lists | — |
| M5SE-T7 | 2026-11-09 | feature | closed | Company export/import (mechs, pilots, inventory) between campaigns, and editable campaign date | 709d622 |
| M5SE-T8 | 2026-11-09 | feature | open | Localization of the editor UI (de, es, fr, ja, ru) and a Settings page (image downloads on/off, cache clear) | — |
| M5SE-T9 | 2026-11-09 | chore | closed | Create mindattic/Mods.Mech5Mercs.SaveEditor on GitHub, point origin at it and push; credit JJ's MW5 Merc Save Editor as inspiration (not code) | — |
| M5SE-T10 | 2026-11-09 | chore | open | After T3-T8 land: package the editor as a professional release and publish it on NexusMods via the shared /publish pipeline (GitHub release + Nexus API), as done for Mods.RogueTrader.Overwatch | — |
| M5SE-T11 | 2026-11-09 | chore | closed | Rename namespaces, projects, folders and solution from Mw5SaveEditor.* to Mech5Mercs.SaveEditor.* to match the Mods.Mech5Mercs.SaveEditor repo | — |
| M5SE-T12 | 2026-11-09 | fix | closed | Fails on main: the build — C:\Program Files\dotnet\sdk\11.0.100-rc.3.26522.148\NuGet.targets(523,5): error MSB3202: The project file "D:\Projects\MindAttic.worktrees\MW5-SaveEditor\main-tests\shared\src\Mech5.Shared\Mech5.Shared.csproj" was not found. [D:\Projects\MindAttic.worktrees\MW5-SaveEditor\main-tests\Mech5.Mercs.S... (landed on main since the last green run: tickets M5SE-T11, M5SE-T3 in 1eab840 Move the editor onto the shared Mods.Mech5.Shared base and rename it to Mech5.Mercs.SaveEditor (M5SE-T11); 71987bb Make HangarEditor.ReadLoadout static so the core builds again (M5SE-T3); 9837051 Add hardpoint classes, a live-loadout reader and apply/export/import plumbing for the loadout editor (M5SE-T3)) | 77a2fea |
| M5SE-T13 | 2026-11-09 | feature | closed | Editor-written saves carry a harmless top-level stamp property; game-written saves are never overwritten - edits always go to a new GUID save registered in Campaign.json; in-place saves only for stamped files, enforced again in the low-level writer | — |
| M5SE-T14 | 2026-11-09 | feature | closed | Theme picker (Mercs default, Clans available) remembered between runs; settings become a plain settings.json beside the exe (no MindAttic folders); image cache moves to %LOCALAPPDATA%\Mech5SaveEditor | 00f8597 |
| M5SE-T15 | 2026-11-09 | feature | open | Try to verify editor-written saves in the real game without a human: launch MW5 Mercenaries headless (-nullrhi/-unattended), load a given save and confirm from the game log that it loaded without errors; report feasibility if the game can't be driven that way | — |
