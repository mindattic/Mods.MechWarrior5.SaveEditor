# Mods.MechWarrior5.Shared

The shared base for MindAttic's MechWarrior 5 save editors:

- [Mods.MechWarrior5.SaveEditor](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor) (MechWarrior 5: Mercenaries)
- Mods.MechWarrior5.SaveEditor (MechWarrior 5: Clans, planned)

Each editor includes this repo as a git submodule at `shared/`.

| Project | What it is |
| --- | --- |
| `src/MechWarrior5.Shared` | Lossless Unreal Engine tagged-property engine: `FString`/`FText`, typed property tree, a serializer that round-trips byte-for-byte with raw fallbacks for anything unknown, and `Ue` builders for the struct shapes MW5 writes (`ItemId`, `PrimaryAssetId`, typed asset ids). |
| `src/MechWarrior5.Shared.Wpf` | MW5 HUD-styled WPF kit: `Theme/MechWarrior5Theme.xaml` with two palettes (Mercs: gunmetal/amber, Clans: steel/ice-cyan) that `ThemeManager.Apply` swaps live, Rajdhani type, buttons, inputs, lists, scrollbars, sliders; `ChamferBorder` panels, the `CheckList` multi-select behavior (row checkboxes, Shift+click ranges, Ctrl+A, Space), Sarna.net 'Mech art (`MechArt` + `MechImageService`), themed dialogs and converters. |
| `tests/MechWarrior5.Shared.Tests` | NUnit tests for the engine. |

## Using it in an editor

```xml
<!-- Core library -->
<ProjectReference Include="..\..\shared\src\MechWarrior5.Shared\MechWarrior5.Shared.csproj" />
<!-- WPF app -->
<ProjectReference Include="..\..\shared\src\MechWarrior5.Shared.Wpf\MechWarrior5.Shared.Wpf.csproj" />
```

```xml
<!-- App.xaml -->
<ResourceDictionary Source="pack://application:,,,/MechWarrior5.Shared.Wpf;component/Theme/MechWarrior5Theme.xaml" />
```

Rajdhani is © Indian Type Foundry, licensed under the SIL Open Font License 1.1 (`src/MechWarrior5.Shared.Wpf/Assets/Fonts/OFL.txt`).
'Mech images are fetched from Sarna.net at runtime and never bundled. This is an unofficial fan project.
