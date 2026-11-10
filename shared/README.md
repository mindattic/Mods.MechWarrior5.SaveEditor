# Mods.Mech5.Shared

The shared base for MindAttic's MechWarrior 5 save editors:

- [Mods.Mech5.Mercs.SaveEditor](https://github.com/mindattic/Mods.Mech5.Mercs.SaveEditor) (MechWarrior 5: Mercenaries)
- Mods.Mech5.Clans.SaveEditor (MechWarrior 5: Clans, planned)

Each editor includes this repo as a git submodule at `shared/`.

| Project | What it is |
| --- | --- |
| `src/Mech5.Shared` | Lossless Unreal Engine tagged-property engine: `FString`/`FText`, typed property tree, a serializer that round-trips byte-for-byte with raw fallbacks for anything unknown, and `Ue` builders for the struct shapes MW5 writes (`ItemId`, `PrimaryAssetId`, typed asset ids). |
| `src/Mech5.Shared.Wpf` | MW5 HUD-styled WPF kit: `Theme/Mech5Theme.xaml` (palette, Rajdhani type, buttons, inputs, lists, scrollbars, sliders), `ChamferBorder` panels, the `CheckList` multi-select behavior (row checkboxes, Shift+click ranges, Ctrl+A, Space), Sarna.net 'Mech art (`MechArt` + `MechImageService`), themed dialogs and converters. |
| `tests/Mech5.Shared.Tests` | NUnit tests for the engine. |

## Using it in an editor

```xml
<!-- Core library -->
<ProjectReference Include="..\..\shared\src\Mech5.Shared\Mech5.Shared.csproj" />
<!-- WPF app -->
<ProjectReference Include="..\..\shared\src\Mech5.Shared.Wpf\Mech5.Shared.Wpf.csproj" />
```

```xml
<!-- App.xaml -->
<ResourceDictionary Source="pack://application:,,,/Mech5.Shared.Wpf;component/Theme/Mech5Theme.xaml" />
```

Rajdhani is © Indian Type Foundry, licensed under the SIL Open Font License 1.1 (`src/Mech5.Shared.Wpf/Assets/Fonts/OFL.txt`).
'Mech images are fetched from Sarna.net at runtime and never bundled. This is an unofficial fan project.
