# shared/

The game-neutral projects of the MechWarrior 5 Save Editor, used by both its Mercenaries and Clans
editors. They live in this repo like the rest of the code (they were once the separate
Mods.MechWarrior5.Shared repo, now archived).

| Project | What it is |
| --- | --- |
| `src/MechWarrior5.Shared` | Lossless Unreal Engine tagged-property engine (UE4 and UE5 saves, the Clans `!GZIP` archive): `FString`/`FText`, typed property tree, a serializer that round-trips byte-for-byte with raw fallbacks for anything unknown, `Ue` builders for the struct shapes MW5 writes, pak and cooked-asset reading, and the editor stamp. |
| `src/MechWarrior5.Shared.Wpf` | MW5 HUD-styled WPF kit: the Mercs and Clans palettes `ThemeManager.Apply` swaps live, Rajdhani type, MindAttic.Components presets, the `CheckList` multi-select behavior (row checkboxes, Shift+click ranges, Ctrl+A, Space), Sarna.net 'Mech art (`MechArt`, `MechImageService`, the lightbox), themed dialogs and converters. |
| `tests/MechWarrior5.Shared.Tests` | NUnit tests for the engine (part of the main solution). |

Rajdhani is © Indian Type Foundry, licensed under the SIL Open Font License 1.1 (`src/MechWarrior5.Shared.Wpf/Assets/Fonts/OFL.txt`).
'Mech images are fetched from Sarna.net at runtime and never bundled. This is an unofficial fan project.
