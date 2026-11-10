---
codex: 1
project: Mods.MechWarrior5.Shared
code: MM5S
layer: stories
status: living
updated: 2026-11-09
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Mods.MechWarrior5.Shared — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| MM5S-T1 | 2026-11-09 | docs | closed | Start the permanent ticket log in the ticket database | — |
| MM5S-T2 | 2026-11-09 | feature | closed | Seed the shared base from the Mercs editor: Mech5.Shared (Unreal tagged-property engine, FString/FText, struct builders) and Mech5.Shared.Wpf (MW5 HUD theme, chamfer panels, check-list multi-select, Sarna image service, dialogs, converters) | — |
| MM5S-T3 | 2026-11-09 | fix | closed | Controls.xaml styles resolved palette brushes as UnsetValue when nested under Mech5Theme.xaml (window failed to load); make Controls.xaml merge Palette.xaml itself | — |
| MM5S-T4 | 2026-11-09 | feature | closed | Add PickFileToOpen/PickFileToSave (title, filter, default name) to IDialogService for editor import/export features | — |
| MM5S-T5 | 2026-11-09 | feature | closed | Goal (user, 2026-11-09): after the Mercs editor is full-featured, tested and working, build a full-featured MechWarrior 5: Clans save editor (Mods.Mech5.Clans.SaveEditor) on Mods.Mech5.Shared, moving as much code as possible into Shared (save container, editors, catalogs pipeline, app shell, pages) | 9897498 |
| MM5S-T6 | 2026-11-09 | fix | closed | FText.WithDisplayString kept Base-history texts (namespace+key), so the game would show the localized original instead of an edited name; always write a culture-invariant (None-history) text, and add FText.Invariant for new texts | — |
| MM5S-T7 | 2026-11-09 | feature | closed | Two themes in Mech5.Shared.Wpf (Mercs: gunmetal/amber; Clans: steel/ice-blue) with a ThemeManager that swaps palettes live (styles use DynamicResource brushes); each app defaults to its game's theme | 9e65ee1 |
| MM5S-T8 | 2026-11-09 | fix | closed | ThemeManager replaced the palette nested inside Mech5Theme.xaml, which raises no resource-changed notification, so elements built before a swap kept the old palette; apply the palette as a top-level Application.Resources merged dictionary instead | a4c5baa |
| MM5S-T9 | 2026-11-09 | feature | closed | Read MechWarrior 5 game and mod data directly: PakFile (UE4 pak v8-v11 index, full directory index, uncompressed/zlib entries) and CookedPackage (cooked .uasset/.uexp: names, imports, exports, FName-tagged properties into the shared PropertyList model) | 75330e2 |
| MM5S-T10 | 2026-11-09 | fix | closed | Theme fonts read a little small: raise every font size in Controls.xaml and MessageDialog.xaml by two points (user request via M5SE-T18) | 267fca2 |
| MM5S-T11 | 2026-11-09 | chore | closed | Rename to Mods.MechWarrior5.Shared: namespaces, projects, folders, comments and docs move from Mech5.* to MechWarrior5.* | 9897498 |
| MM5S-T12 | 2026-11-09 | feature | closed | Add Glyph.Skull and Glyph.ClanStar theme icons to the shared theme (used by the save editor's theme button, M5SE-T35) | bba0d8c |
| MM5S-T13 | 2026-11-09 | chore | closed | Remove the Glyph.Skull and Glyph.ClanStar icons again (the editor dropped its theme button, M5SE-T36) | 2175b03 |
| MM5S-T14 | 2026-11-10 | feature | closed | Mirror from the save editor: WPF controls come from MindAttic.Components (MaButton presets, MaShapeBorder Panel, ChamferBorder removed, Components 37.0.1 + nuget.config), Button.Picture preset, MechArtLightbox (MaModal) with MechArt.ShowCommand, MechImageService sized pictures, UI-thread-safe image loads | 5d1d19a |
| MM5S-T15 | 2026-11-10 | chore | closed | Crash recovery: commit the command-catalog sync (.agent/commands/*.md) and regenerated docs/USER_STORIES.md left uncommitted by the session crash | — |
