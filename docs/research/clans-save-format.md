# MechWarrior 5: Clans save format

Groundwork for the Clans editor (`Mods.MechWarrior5.SaveEditor`, ticket MM5S-T5). Everything here was
verified against 24 real campaign saves and 4 metadata files from a Steam install (game build of
November 2026). The prototype readers that established it live in [`clans-tools/`](clans-tools/).

## Where the files are

`%LOCALAPPDATA%\MW5Clans\Saved\SaveGames\`

| File | What it is |
| --- | --- |
| `<ProfileKey>_<SaveGuid>.sav` | One campaign save. `ProfileKey` is the campaign profile, e.g. `F206262946D44FC91A3FBC854139FBE4`. |
| `UserSaveMetaData.sav`, `UserSaveMetaData1..3.sav` | The load-menu index (the Clans counterpart of Mercs' `Campaign.json`), written round-robin. |
| `steam_autocloud.vdf` | Steam Cloud bookkeeping; ignore. |

## Container: `!GZIP` + Unreal compressed chunks

Every `.sav` (saves and metadata alike) is:

```
"!GZIP"                                  5 bytes magic
repeat until EOF:                        one block per 128 KiB of payload
  uint64 PackageFileTag = 0x222222229E2A83C1
  uint64 ChunkSize      = 0x20000
  uint8  Compressor     = 4              (gzip)
  uint64 CompressedSize, UncompressedSize          block summary
  uint64 CompressedSize, UncompressedSize          per chunk (always one chunk per block here)
  byte[] gzip stream (RFC 1952, decompress with wbits=31)
```

Recompressing the payload this way (gzip level 6, one 128 KiB chunk per block) and unpacking again
round-trips all 28 files byte-for-byte. The compressed bytes themselves need not match the game's.

## Payload

Campaign saves: `int32 PayloadLength (= total - 4)`, a `0x00` byte, then a tagged property list.
Metadata files: a standard UE5 `GVAS` header (engine `UE5`, custom-version table) then
`/Script/MechWarrior.KelUserSaveMetaData` and its property list.

### UE 5.4+ property tags (the big difference from Mercs)

Mercs (UE 4.26) writes `Name, Type, Size, ArrayIndex, type-specific data, HasGuid`. Clans writes:

```
FString Name                    ("None" ends the list)
FPropertyTypeName Type          recursive: FString Name, int32 ParamCount, ParamCount x FPropertyTypeName
int32   Size
uint8   Flags                   EPropertyTagFlags
  0x01 HasArrayIndex  -> int32 ArrayIndex follows
  0x02 HasPropertyGuid -> FGuid follows
  0x04 HasPropertyExtensions -> uint8 ext (+ uint8 if ext & 0x02)
  0x08 HasBinaryOrNativeSerialize (native struct: Guid, LinearColor, DateTime, GameplayTag...)
  0x10 BoolTrue  (BoolProperty value lives here; Size is 0)
value (Size bytes)
```

Type examples as they appear in saves:

- `StructProperty(PrimaryAssetId(/Script/CoreUObject))`
- `ArrayProperty(StructProperty(KelItemDataAssetId(/Script/MechWarrior)))`
- `EnumProperty(EMWHealthState(/Script/MechWarrior),ByteProperty)`, whose value is an FString such as `EMWHealthState::Healthy`
- `MapProperty(StructProperty(Guid(/Script/CoreUObject)),StructProperty(MissionLogEntry(/Script/MechWarrior)))`

Struct values carry no struct-name or GUID prefix any more (the type tree has it). Arrays of
non-native structs are `int32 Count` followed by `Count` property lists, each ending in `None`.

### Object blobs

`PersistentModelData`, `InventoryData`, `DeploymentData` and every `ByteData` are
`ArrayProperty(ByteProperty)` holding a nested object stream:

```
int32 Count (= Size - 4)
[FString ClassPath]      present when the first byte is non-zero (e.g. InventoryData)
uint8 0x00
property list
```

Inline objects inside a property list (each model in `PersistentModelData`) are `ObjectProperty`
whose value is `FString ClassPath`, `0x00`, then that object's property list.

## What a campaign save contains

Top level: `GameModeClass` (`/Game/Maps/Campaign/MissionHub/KelCampaignMode.KelCampaignMode_C`),
`PersistentModelData`, `DeploymentData`, `InventoryData`.

### `InventoryData`: everything the player owns

An `ActorSaveDataArray` with `Array: [ActorSaveData{ActorClass, ByteData}]`. In the sample (Act 2):

| Count | ActorClass | ByteData fields | Editable as |
| --- | --- | --- | --- |
| 268 | `KelStackableInventoryItem` | `Quantity`, `ItemData{InventoryId, AssetId, InventoryTag}` | Currencies, MXP, salvage, crew, weapons and equipment |
| 68 | `KelMechInventoryItem` | `Loadout` (MechLoadout), `ItemData` | Mechs |
| 11 | `KelPersistentUnlockItem` | `DataAssetId`, `ItemData` | Chassis purchase unlocks |
| 6 | `KelPilotInventoryItem` | `ItemData` (asset `Nasir_PilotItem`, ...) | Pilots (fixed story roster) |

Stackables by asset (`PrimaryAssetName`), sample totals:

- `Kerenskies_CurrencyItem` (money, 18.7 M), `Honor_CurrencyItem`, `Merits_CurrencyItem`
- `MXP_<Chassis>` (per-chassis mech XP, e.g. `MXP_NovaMech`)
- `Salvage_EquipmentComponents`, `Salvage_WeaponComponents`, `Salvage_MechComponents`
- `Technician_CrewItem`, `Scientist_CrewItem`, `*Upgrade_CrewItem`
- `Clan_ERMediumLaser`, `Clan_DoubleHeatsink`, `Clan_GaussAmmo`, ... (weapons and equipment)

`Quantity` is an `IntProperty` (Size 4), so changing a stack is a same-size in-place edit. Adding a new
stack means appending an `ActorSaveData` with a fresh `InventoryId` GUID. The `Array` count and every
enclosing Size then grow, which needs a real serializer rather than a patch.

Mech `Loadout` fields: `MechItemDataAssetId` (`KelMechItemDataAsset` / `VPR_MIA`), `CustomName`,
`SkinCustomization`, `CurrentStructure`, `CurrentArmor`, `CurrentRearArmor`, `InstalledArmor`,
`InstalledRearArmor` (per-location floats), `WeaponGroupInfo`, `EquippedOmnipods`,
`InstalledEquipment`, `PresetHintInvId`. Repair means copying Installed* into Current* and restoring
structure.

### `PersistentModelData`: campaign state (29 models)

Notable models:

- `MetaLevelingModel.LevelProgressions[]`: Honor level (`HonorLevelingAsset`: `TotalEXP`, `CurrentDisplayLevel`).
- `ResearchJobModel`: `ResearchJobQueue[]` (`ResearchPointsApplied`), `CompletedResearchJobs[]`.
- `TechnicianAssignmentModel`: `TotalAvailableTechs`, `MaxAssignableTechsPerMech`, `TechAssignments[]`.
- `CycleTimeModel.TotalCyclesElapsed`.
- `SalvageModel.SalvageResourceRecords`.
- `MechPlanningModel.PlanDetails[]`.
- `DifficultyModel.DifficultySettings`.
- `MarketModel.SellMultipliers[]`.
- `MechUsageModel.MechIdsUsed[]`.
- `PostMissionModel`, `DialogueModel`, `AchievementModel`, `MissionHistoryModel`, `StatusReportModel`.
- `SaveStateModel.PersistentActors[]` (`ActorSaveData`, as in Mercs).

Pilots have no skill trees in the save; their progression is campaign-scripted.

### `DeploymentData`

`DeployedMechs.List[]` of `DeployedMechEntry{Pilot{PlayerIdentifier}, MechInventoryId, PilotInventoryId, ...}`.
This is the five-mech star. Each slot references inventory GUIDs, so a mech that is deployed must
never be removed.

## `UserSaveMetaData`: the load menu

`UserProfiles[]` (`KelUserProfileData{ProfileKey, ProfileInfo{Name, LastSaveFileName, CreationDate,
LastSaveDate, StartConditionId, DifficultyId}}`) and `SaveHeaders[]` of `MWSaveHeader`:

- Identity: `SaveFileName` (the GUID part), `Name` (TextProperty), `GameTimeElapsed`, `SaveType` (`EMWSaveType::ManualSave`/`AutoSave`), `bIsMissionSave`, `SaveDate`.
- Load-menu summary: `Kerenskies`, `Merits`, `Honor`, `DeployedMechs` (`KelMechData`), `PilotAssetIds`.
- Other: `CustomSaveTag`, `bHideFile`, `NewGamePlusCount`, `OwnedDLCs` (`DLC01_0_GhostBear`, `DLC2_ClanWolf_Entitlement`), `LocaleName`, `AllowedTonnage`, `Difficulty`, `TitleScreenDataLayerAssetId`, `MissionName`/`PreviousMissionName`.

The four metadata files rotate. Editing the newest one alone risks the game reading a sibling, so a
new save should be registered in all four (keeping `.bak` copies).

## How the Mercs safety rules map to Clans

| Mercs | Clans |
| --- | --- |
| Never overwrite an original save; write a new GUID file | Same. New `<ProfileKey>_<NewGuid>.sav`. |
| `Mech5SaveEditorStamp` StrProperty in the save | Same property name, appended to the top-level list (unknown properties are skipped by UE). |
| Register the copy in `Campaign.json` with a custom name | Add an `MWSaveHeader` to `SaveHeaders` (copy of the source's header, new `SaveFileName`, custom `Name`, refreshed Kerenskies/Merits/Honor), optionally `CustomSaveTag` = editor marker. |
| Asset validation against the game paks | Clans ships IoStore (`.utoc`/`.ucas`), so the Shared `PakFile` reader needs an IoStore TOC reader for the asset-name list. |

## What Shared needs for Clans

1. `UeVersion` switch in the property serializer: UE4 tag (Mercs) vs UE5.4 tag (FPropertyTypeName + flags).
2. Object-blob reader (`[class] 0x00 props`) shared by both games (Mercs' blobs are the UE4 variant).
3. `!GZIP` chunked container reader and writer (Mercs saves are uncompressed).
4. GVAS UE5 header read and write for `UserSaveMetaData`.
5. IoStore TOC reader for asset validation and the game-data scan.
