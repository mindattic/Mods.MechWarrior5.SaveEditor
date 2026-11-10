# Mods.Mech5.Mercs.SaveEditor

**MW5 Mercs Save Editor 2.0**

A save editor for **MechWarrior 5: Mercenaries**, rebuilt from scratch: a lossless save engine, every DLC
through **DLC 9 - Ashes & Ascension**, and an interface styled after the game's own HUD.

## Features

- **Pick saves by name.** Campaigns and saves are listed by their in-game names, dates, systems and
  reputation (read from each campaign's `Campaign.json`), not by GUID file names.
- **Mech logistics**
  - A requisition list of all 639 variants with Sarna.net artwork, weight class, tonnage, hero names,
    DLC source and how many you already own.
  - A checkbox on every row. **Shift+click** ticks or clears a whole range, **Ctrl+A** or
    **Select all shown** picks everything the filters show, and the header checkbox toggles the visible
    set.
  - Filter by search text, weight class, hero mechs, DLC pack, or *owned DLC only*.
  - **Add N to cold storage** in one click, with optional copies of each. Every mech arrives with its
    real factory loadout (armor, structure, weapons, weapon groups and equipment) and the right paint
    scheme. One summary covers the whole batch: added and skipped, with reasons.
  - The company hangar lists bay and cold-storage mechs, and can repair, reset to the stock loadout, or
    remove cold-storage mechs, all in batches.
- **Inventory:** add any weapon, equipment or ammo at any tier in bulk, set counts for many stacks at
  once, and remove stacks.
- **Pilots:** skill caps (sliders), XP per skill, salary, heal, max all caps, heal all.
- **Factions:** standing meters from -100 to +100, plus set-all-allied or set-all-neutral.
- **Company:** C-Bills (with quick +1M/+10M/+100M/Max), reputation, company name, campaign date, and the
  DLC status of the campaign.
- **DLC aware.** Mechs from DLC the campaign doesn't have are flagged and refused, so a save can't be
  broken by content the game can't load.

## Safety

- **Lossless or nothing.** Every save is parsed and re-serialized before editing. If the result is not
  byte-for-byte identical to the file on disk, the editor refuses to edit it. Every MW5 campaign save
  tested so far (2022-2026, game v1.15) round-trips exactly.
- Saves are written to a temp file and swapped in atomically, and the previous file is kept as
  `<name>.sav.bak`.
- Embedded mech loadouts that don't round-trip are shown but never rewritten.
- Default-valued fields that Unreal omits, such as a zero standing or reputation, are only created
  when you actually edit them.

## Using it

1. Close the save in-game (stay on the main menu, or quit).
2. Open the editor, pick a campaign and double-click a save. **Browse** also works for any `.sav` file.
3. Make your changes, then **Save** (Ctrl+S). Load the same save in the game.

Saves live in `%LOCALAPPDATA%\MW5Mercs\Saved\SaveGames\<campaign>`. Saving in place keeps the save
listed in the game's load menu.

## Building

Requires the .NET 10 SDK on Windows. The shared engine and UI kit come from
[Mods.Mech5.Shared](https://github.com/mindattic/Mods.Mech5.Shared), included as a git submodule, which a
future Clans editor will also use.

```powershell
git clone --recurse-submodules https://github.com/mindattic/Mods.Mech5.Mercs.SaveEditor.git
dotnet build Mech5.Mercs.SaveEditor.sln -c Release
dotnet run --project src/Mech5.Mercs.SaveEditor.App
```

| Project | What it is |
| --- | --- |
| `shared/src/Mech5.Shared` | Lossless Unreal property-stream engine (submodule) |
| `shared/src/Mech5.Shared.Wpf` | MW5 HUD theme, check-list multi-select, Sarna art, dialogs (submodule) |
| `src/Mech5.Mercs.SaveEditor.Core` | MW5 Mercenaries save model, game editors, catalogs |
| `src/Mech5.Mercs.SaveEditor.App` | WPF/MVVM desktop app (CommunityToolkit.Mvvm) |
| `tests/Mech5.Mercs.SaveEditor.Core.Tests` | NUnit tests; `RealSaveTests` run against copies of real saves in `MW5_SAVE_DIR` |

## How the save format works

An MW5 campaign save is a header-less Unreal tagged-property stream. Its `PersistentModelData` byte
array embeds every game model (`InventoryModel`, `RosterModel`, `MercCompanyModel`,
`SaveStateModel`, ...) as an inline object. Each 'Mech is an `ActorSaveData` entry in
`SaveStateModel.MechLoadoutWrappers`, whose `ByteData` holds another serialized property list:
`MechLoadoutType` plus `MarketItemMech.Item` (`ItemId` + `MechLoadout`). Hangar bays reference mechs by
item GUID through `MechStorageModel.MechStorageList`. Cold-storage mechs carry
`EMechLoadoutType::ColdStorageLoadout` and are not referenced by a bay.

## Credits

- Inspired by JJ's [MW5 Merc Save Editor](https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466).
  No code from it is used; this editor was written from scratch.
- 'Mech art is fetched from [Sarna.net](https://www.sarna.net) at runtime and is never bundled.
- The Rajdhani font is under the SIL OFL.
- Full notices, including data catalog licensing: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

This is an unofficial fan tool, not affiliated with Piranha Games.
