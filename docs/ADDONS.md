# Add-on packs: format reference

MechWarrior 5: Mercenaries only. For pack authors.

The editor already reads every enabled mod's own files: its weapons, equipment, ammo, traits, 'Mechs,
pictures and pilot skill curve. An **add-on pack** carries what those files don't say, or says
differently: limits, names, changed values, and the list of everything the mod adds. That list also lets
the editor tell a player which mod a save needs when that mod isn't installed.

A pack is one plain JSON file (UTF-8) in the `Addons` folder beside `MW5SaveEditor.exe`. Packs are data
only; nothing in them runs.

## Example

```json
{
  "schema": 1,
  "name": "Yet Another Weapon - Complete Edition",
  "author": "DeadRaiser, cszolee, and LittleTex115",
  "game": "mercenaries",
  "source": "https://www.nexusmods.com/mechwarrior5mercenaries/mods/1158",
  "requires": {
    "modName": "Yet Another Weapon - Complete Edition",
    "modFolder": "YetAnotherWeaponCompleteEdition",
    "versions": ["3.0"]
  },
  "requiresPacks": [],
  "limits": { "maxSkillCap": 30 },
  "skillCurve": [ { "level": 10, "xp": 14110 }, { "level": 30, "xp": 120000 } ],
  "pilotTraits": { "add": [ { "asset": "YAW_Trait", "label": "Gunslinger", "description": "..." } ], "override": [] },
  "mechTraits": { "add": [], "override": [] },
  "items": {
    "add": [ { "asset": "CLRM10_ArtemisV", "type": "MWMissileWeaponDataAsset", "kind": "weapon",
               "label": "Clan Long-Range Missile 10 + Artemis V", "hardpoint": "M" } ],
    "override": [ { "asset": "MediumLaser", "label": "Medium Laser (YAW)" } ]
  },
  "mechs": { "add": [ { "asset": "BJ-1X_MDA", "code": "BJ-1X", "chassis": "Blackjack", "name": "Blackjack", "tons": 45 } ], "override": [] },
  "assets": [ "YAWglobals_instanced" ]
}
```

## Fields

Only `schema`, `name` and `requires` are required. Unknown fields are ignored, so newer packs still load.

| Field | Meaning |
| --- | --- |
| `schema` | Format version. This editor reads `1`; a higher number is refused with "needs a newer editor". |
| `name` | The pack's name, shown in Settings. Other packs refer to it in `requiresPacks`. |
| `author` | Who made the mod (or the pack). Shown for credit. |
| `game` | `mercenaries` (the default). Any other value is reported as "Not for this game" and ignored. |
| `source` | The mod's page (`https://` only). Shown in Settings and in the compatibility report. |
| `requires.modName` | The mod's `mod.json` `displayName`. Matched first, case-insensitively. |
| `requires.modFolder` | The mod's folder name (its `modlist.json` key), used when the name doesn't match. Folder names aren't reliable (YAEC-R installs as `harjel`), so give both. |
| `requires.versions` | Versions the pack supports, as prefixes: `"3.0"` accepts `3.0g`. Empty accepts any version. |
| `requiresPacks` | Names of other packs that must also be active (for example an add-on mod's pack needing its base mod's pack). |
| `limits` | Limits the mod allows; see below. |
| `skillCurve` | `[{ "level", "xp" }]`, the mod's pilot skill levels. Its top level counts as `maxSkillCap` and its highest XP as `maxExperience`. |
| `pilotTraits`, `mechTraits` | `add` and `override` lists of `{ "asset", "label", "description" }`. |
| `items` | `add` and `override` lists of `{ "asset", "type", "kind", "label", "hardpoint", "ammoType" }`. `type` is the data asset class (`MWBeamWeaponDataAsset`, `MWAmmoDataAsset`, ...), `kind` is `weapon`, `equipment` or `ammo`, `hardpoint` is `E`, `B`, `M`, `A` or `Melee`, and `ammoType` is the game's ammo tag (`Ammo.Missile.LRM`): a weapon and the ammo with the same tag go together when adding ammo with weapons, so an override can fix a mod whose weapon and ammo tags don't match. An added item needs `type` and `kind`. |
| `mechs` | `add` and `override` lists of `{ "asset", "code", "chassis", "name", "tons", "hero" }`. An added 'Mech needs `tons` and a `name` or `chassis`. |
| `assets` | Other asset names of the mod a save may reference (campaign actors, skins, decals). Never offered; used only to name the mod in the compatibility report. |

**`add` and `override`.** `add` brings in assets the base game doesn't have. `override` changes fields of
an entry that already exists (built in, read from a mod, or from an earlier pack), matched by `asset`:
only the fields you give are replaced. Packs never remove entries.

### Limits

| Key | Vanilla |
| --- | --- |
| `maxSkillCap` | 10 (from the game's own skill curve) |
| `maxExperience` | 1,000,000 |
| `maxSalary` | 10,000,000 |
| `maxCBills` | 2,000,000,000 |
| `minStanding` | -100 |
| `maxStanding` | 100 |
| `maxCount` | 9,999 (items per stack) |

Limits only widen: for every key, the widest value among the active packs (and an enabled mod's own
skill curve) wins. A `max` key widens upwards and `minStanding` widens downwards, so one pack raising a
limit is never undone by another that doesn't mention it. A key no active pack sets stays vanilla.

## When a pack applies

The editor reads every `*.json` in `Addons` at startup and on **Rescan game and mods**. A file it can't
read is listed with the reason and changes nothing. Each pack gets one status:

| Status | When |
| --- | --- |
| Active | The mod is installed (it has paks), enabled in the game's `modlist.json`, and a listed version. |
| Mod not installed | No installed mod matches `requires`. |
| Mod disabled in the game | The mod is installed but not enabled (a mod missing from `modlist.json` counts as disabled). |
| Mod version X not supported | The installed version matches no prefix in `versions`. |
| Needs the "X" add-on | A pack named in `requiresPacks` isn't active. |
| Turned off | The player unticked it in Settings (or ticked "Turn all add-ons off"). |
| Not for this game | `game` isn't `mercenaries`. |
| Can't be read | Bad JSON, or `schema`, `name` or `requires` missing. |

Active packs apply in the game's mod load order (`defaultLoadOrder`); where two packs change the same
asset, the later one wins, as in game, and Settings lists the collision.

## Safety

- With no packs, or none active, the editor behaves exactly as vanilla.
- Packs only change what the editor offers and how far it lets you edit. Nothing is written into a save
  because a pack is on or off.
- Turning a pack off restores the built-in values exactly: packs are layered over the built-in data, never
  written into it.
- A save value already beyond the active limits is kept as it is; the editor only stops an edit from pushing
  it further.

## Fixing one mod

The editor reads every enabled mod from its own files, so most mods need no pack. When a mod's own data is
wrong or missing something, an `override` in a pack fixes just that, only while the mod is active. For
example, a weapon whose ammo tag matches no ammo in the mod (so "+ ammo" adds nothing), with a clearer
name:

```json
{
  "schema": 1,
  "name": "Yet Another Weapon - Complete Edition",
  "requires": { "modName": "Yet Another Weapon - Complete Edition", "versions": ["3.0"] },
  "items": {
    "override": [
      { "asset": "YAOW_BearHunterAC", "ammoType": "Ammo.Ballistic.AutoCannon.Standard.AC20", "label": "Bear Hunter Autocannon" }
    ]
  }
}
```

Only the fields an override gives are replaced; turning the pack off restores the mod's own values. Found a
mod the editor gets wrong? Open an issue on
[GitHub](https://github.com/mindattic/Mods.MechWarrior5.SaveEditor/issues) with the mod's name and version
(Settings, Add-ons shows both) and what looks wrong; a fix usually ships as a pack, no new editor needed.

## Making a pack

Don't write a pack by hand: generate it from the mod's own files with `tools/AddonPack`, then review it.

```powershell
# A mod folder (with mod.json and Paks) or a folder holding several, e.g. a Vortex staging folder.
dotnet run --project tools/AddonPack -- "<mod folder>" --out addons --source https://www.nexusmods.com/mechwarrior5mercenaries/mods/<id>

# Download a mod's main file from NexusMods first (.zip only). Needs NEXUSMODS_API_KEY from a Premium
# account, since the NexusMods API gives download links only to Premium accounts.
dotnet run --project tools/AddonPack -- nexus:<modId> --out addons
```

The tool finds the game with the same detection as the editor (or `--game <MW5 folder>`), reads the mod's
paks against the base game, and writes one pack per `mod.json`: the mod's items, traits and 'Mechs (assets
the base game doesn't have, with their in-game names), its skill curve's top level when it ships one, and
its other referenceable asset names. `requires` is filled from `mod.json`, with the installed version.
