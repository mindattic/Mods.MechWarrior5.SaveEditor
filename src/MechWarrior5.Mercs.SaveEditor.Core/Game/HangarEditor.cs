using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

public enum MechLocation
{
    MechBay,
    ColdStorage,
    Market,
    Unassigned,
}

/// <summary>
/// Every 'Mech the company owns or is offered. Each lives in <c>SaveStateModel.MechLoadoutWrappers</c>
/// as an actor whose <c>ByteData</c> embeds its serialized loadout; hangar bays reference mechs by
/// item id through <c>MechStorageModel.MechStorageList</c>.
/// </summary>
public sealed class HangarEditor
{
    private readonly StructArrayStorage? _wrappers;
    private readonly HashSet<Guid> _bayIds;
    private readonly MechCatalog _catalog;
    private readonly DlcOwnership _dlc;
    private readonly Action _changed;
    private readonly List<MechRecord> _mechs = [];

    internal HangarEditor(Mw5SaveFile file, MechCatalog catalog, DlcOwnership dlc, Action changed)
    {
        _catalog = catalog;
        _dlc = dlc;
        _changed = changed;
        _wrappers = file.GetModel("SaveStateModel").Find<ArrayProperty>("MechLoadoutWrappers")?.Storage as StructArrayStorage;
        var storage = file.TryGetModel("MechStorageModel")?.Find<ArrayProperty>("MechStorageList")?.Storage as StructArrayStorage;
        _bayIds = storage?.PropertyElements
            .Select(Ue.ItemGuid)
            .OfType<Guid>()
            .Where(g => g != Guid.Empty)
            .ToHashSet() ?? [];

        if (_wrappers is not null)
        {
            foreach (var wrapper in _wrappers.PropertyElements)
            {
                if (MechRecord.TryRead(wrapper, _bayIds, catalog) is { } record)
                {
                    _mechs.Add(record);
                }
            }
        }
    }

    public IReadOnlyList<MechRecord> Mechs => _mechs;

    public bool CanAdd => _wrappers is not null && _mechs.Count > 0;

    public int BayCapacity => _bayIds.Count;

    public BatchResult<MechVariant> AddToColdStorage(IEnumerable<MechVariant> variants, int copiesEach = 1)
    {
        var result = new BatchResult<MechVariant>();
        var template = _mechs.FirstOrDefault(m => m.IsEditable);
        copiesEach = Math.Clamp(copiesEach, 1, 50);
        foreach (var variant in variants)
        {
            if (_wrappers is null || template is null)
            {
                result.Skip(variant, "this save has no hangar data to copy the mech layout from");
                continue;
            }

            if (_dlc.Source != DlcSource.Unknown && !_dlc.Owns(variant.Dlc))
            {
                result.Skip(variant, $"needs the {variant.Dlc!.Name} DLC, which this campaign doesn't have");
                continue;
            }

            if (_catalog.FindStock(variant.Asset) is not { } stock)
            {
                result.Skip(variant, "no stock loadout data for this variant");
                continue;
            }

            for (var i = 0; i < copiesEach; i++)
            {
                Append(template, variant.Asset, variant, SkinFor(variant), stock);
            }

            result.Succeed(variant);
        }

        if (result.Succeeded.Count > 0)
        {
            _changed();
        }

        return result;
    }

    /// <summary>
    /// Adds one mech with the given loadout (e.g. from another campaign) to cold storage; returns why
    /// it couldn't, or null on success.
    /// </summary>
    public string? AddLoadoutToColdStorage(string asset, string? skin, StockLoadout loadout)
    {
        var template = _mechs.FirstOrDefault(m => m.IsEditable);
        if (_wrappers is null || template is null)
        {
            return "this save has no hangar data to copy the mech layout from";
        }

        var variant = _catalog.FindByAsset(asset);
        if (variant is not null && _dlc.Source != DlcSource.Unknown && !_dlc.Owns(variant.Dlc))
        {
            return $"needs the {variant.Dlc!.Name} DLC, which this campaign doesn't have";
        }

        var resolvedSkin = skin is { Length: > 0 } ? skin : variant is null ? null : SkinFor(variant);
        if (resolvedSkin is null)
        {
            return "unknown variant with no paint scheme to give it";
        }

        Append(template, asset, variant, resolvedSkin, loadout);
        _changed();
        return null;
    }

    /// <summary>Removes cold-storage mechs. Hangar-bay mechs stay: lances and pilots reference them.</summary>
    public BatchResult<MechRecord> RemoveFromColdStorage(IEnumerable<MechRecord> mechs)
    {
        var result = new BatchResult<MechRecord>();
        foreach (var mech in mechs.ToList())
        {
            if (mech.Location != MechLocation.ColdStorage)
            {
                result.Skip(mech, "only cold-storage mechs can be removed");
                continue;
            }

            if (_wrappers?.Elements.Remove(mech.Wrapper) == true)
            {
                _mechs.Remove(mech);
                result.Succeed(mech);
            }
        }

        if (result.Succeeded.Count > 0)
        {
            _changed();
        }

        return result;
    }

    public BatchResult<MechRecord> ResetToStock(IEnumerable<MechRecord> mechs) =>
        Apply(mechs, (mech, stock) =>
        {
            foreach (var field in MechLoadoutBuilder.BuildStockFields(stock))
            {
                mech.LoadoutData.Set(field);
            }
        });

    /// <summary>Restores armor to its installed values, structure to full, and every part to healthy.</summary>
    public BatchResult<MechRecord> Repair(IEnumerable<MechRecord> mechs) =>
        Apply(mechs, (mech, stock) =>
        {
            var data = mech.LoadoutData;
            if (data.Find<StructProperty>("InstalledArmor") is { } armor)
            {
                var current = armor.DeepClone();
                current.Name = "CurrentArmor";
                data.Set(current);
            }

            if (data.Find<StructProperty>("InstalledRearArmor") is { } rear)
            {
                var current = rear.DeepClone();
                current.Name = "CurrentRearArmor";
                data.Set(current);
            }

            data.Set(MechLoadoutBuilder.Parts("CurrentStructure", stock.Structure));
            MarkHealthy(data);
        });

    public static StockLoadout ReadLoadout(MechRecord mech) => MechLoadoutReader.Read(mech.LoadoutData);

    /// <summary>Traits installed on one 'Mech (<c>MechLoadout.InstalledTraits</c>).</summary>
    public static IReadOnlyList<string> MechTraits(MechRecord mech) =>
        TraitIds.Read(mech.LoadoutData.Find<ArrayProperty>("InstalledTraits")?.Storage as StructArrayStorage);

    /// <summary>Replaces a 'Mech's installed traits.</summary>
    public void SetMechTraits(MechRecord mech, IEnumerable<string> traits)
    {
        var wanted = traits.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.SequenceEqual(MechTraits(mech), StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var data = mech.LoadoutData;
        if (data.Find<ArrayProperty>("InstalledTraits")?.Storage is StructArrayStorage existing)
        {
            existing.Elements.Clear();
        }

        foreach (var trait in wanted)
        {
            TraitIds.Add(data, "InstalledTraits", "MechTraitDataAssetId", trait, insertAfter: "Equipment");
        }

        mech.MarkModified();
        _changed();
    }

    public StockLoadout? StockFor(MechRecord mech) => _catalog.FindStock(mech.Asset);

    /// <summary>
    /// Every weapon hardpoint the mech has: the variant's stock hardpoints plus any the current
    /// loadout uses, in a stable order. Melee mounts are included; they take melee weapons only.
    /// </summary>
    public IReadOnlyList<string> HardpointsFor(MechRecord mech)
    {
        var slots = new List<string>();
        foreach (var slot in (StockFor(mech)?.Weapons.Select(w => w.Slot) ?? [])
                     .Concat(ReadLoadout(mech).Weapons.Select(w => w.Slot)))
        {
            if (!slots.Contains(slot, StringComparer.OrdinalIgnoreCase))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>Replaces the mech's armor, weapons, groups and equipment; identity and skin are kept.</summary>
    public void ApplyLoadout(MechRecord mech, StockLoadout loadout)
    {
        if (!mech.IsEditable)
        {
            throw new InvalidOperationException("This mech's data can't be safely rewritten.");
        }

        // A refit changes only what was refitted: the mech keeps its damage (missing armor per location,
        // damaged or destroyed weapons and equipment left in their slots), as structure already does.
        var data = mech.LoadoutData;
        var armorDamage = Damage(data, "InstalledArmor", "CurrentArmor");
        var rearDamage = Damage(data, "InstalledRearArmor", "CurrentRearArmor");
        var health = ItemHealth(data).Where(kv => !kv.Value.Value.Value!.EndsWith("::Healthy", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var field in MechLoadoutBuilder.BuildStockFields(loadout))
        {
            data.Set(field);
        }

        ApplyDamage(data, "InstalledArmor", "CurrentArmor", armorDamage);
        ApplyDamage(data, "InstalledRearArmor", "CurrentRearArmor", rearDamage);
        foreach (var (key, state) in ItemHealth(data))
        {
            if (health.TryGetValue(key, out var kept))
            {
                state.Value = kept;
            }
        }

        mech.MarkModified();
        _changed();
    }

    /// <summary>Armor missing per location: installed minus current (0 for an undamaged location).</summary>
    private static Dictionary<string, float> Damage(PropertyList data, string installed, string current)
    {
        var full = Floats(data, installed);
        var now = Floats(data, current);
        return full.ToDictionary(kv => kv.Key, kv => Math.Max(0, kv.Value - now.GetValueOrDefault(kv.Key, kv.Value)));
    }

    private static void ApplyDamage(PropertyList data, string installed, string current, Dictionary<string, float> damage)
    {
        var full = Floats(data, installed);
        if (data.Find<StructProperty>(current)?.Value is not PropertyList parts)
        {
            return;
        }

        foreach (var part in parts.OfType<FloatProperty>())
        {
            part.Value = Math.Max(0, full.GetValueOrDefault(part.Name) - damage.GetValueOrDefault(part.Name));
        }
    }

    private static Dictionary<string, float> Floats(PropertyList data, string field) =>
        data.Find<StructProperty>(field)?.Value is PropertyList parts
            ? parts.OfType<FloatProperty>().GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First().Value)
            : [];

    /// <summary>Each fitted weapon's and equipment item's health, keyed by where it sits and what it is.</summary>
    private static Dictionary<string, EnumProperty> ItemHealth(PropertyList data)
    {
        var result = new Dictionary<string, EnumProperty>(StringComparer.OrdinalIgnoreCase);
        if (data.Find<ArrayProperty>("InstalledWeapons")?.Storage is StructArrayStorage weapons)
        {
            foreach (var weapon in weapons.PropertyElements)
            {
                if (weapon.Find<StructProperty>("WeaponData")?.Value is PropertyList wd && wd.Find<EnumProperty>("HealthState") is { } h)
                {
                    result.TryAdd($"W|{weapon.Find<NameProperty>("HardpointSlotID")?.Value}|{Ue.AssetName(wd.Find<StructProperty>("WeaponId")?.Value as PropertyList)}", h);
                }
            }
        }

        if ((data.Find<StructProperty>("Equipment")?.Value as PropertyList)?.Find<ArrayProperty>("MechPartEquipment")?.Storage is StructArrayStorage parts)
        {
            foreach (var part in parts.PropertyElements)
            {
                var where = part.Find<EnumProperty>("MechPart")?.Value.Value?.Split("::")[^1];
                if (part.Find<ArrayProperty>("SlottedEquipment")?.Storage is not StructArrayStorage slots)
                {
                    continue;
                }

                foreach (var slot in slots.PropertyElements)
                {
                    if (slot.Find<StructProperty>("EquipmentData")?.Value is PropertyList ed && ed.Find<EnumProperty>("HealthState") is { } h)
                    {
                        result.TryAdd($"E|{where}|{slot.Find<IntProperty>("SlotId")?.Value}|{Ue.AssetName(ed.Find<StructProperty>("EquipmentId")?.Value as PropertyList)}", h);
                    }
                }
            }
        }

        return result;
    }

    public void Commit()
    {
        foreach (var mech in _mechs)
        {
            mech.Commit();
        }
    }

    private void Append(MechRecord template, string asset, MechVariant? variant, string skin, StockLoadout loadout)
    {
        var blob = MechLoadoutBuilder.BuildColdStorageBlob(asset, skin, loadout, Guid.NewGuid());
        var wrapper = template.Wrapper.DeepClone();
        var record = MechRecord.Create(wrapper, blob, template.Trailing, variant);
        _wrappers!.Elements.Add(wrapper);
        _mechs.Add(record);
    }

    private BatchResult<MechRecord> Apply(IEnumerable<MechRecord> mechs, Action<MechRecord, StockLoadout> action)
    {
        var result = new BatchResult<MechRecord>();
        foreach (var mech in mechs)
        {
            if (!mech.IsEditable)
            {
                result.Skip(mech, "its data uses a layout this editor can't safely rewrite");
            }
            else if (mech.Location == MechLocation.Market)
            {
                result.Skip(mech, "market mechs belong to the store, not your company");
            }
            else if (_catalog.FindStock(mech.Asset) is not { } stock)
            {
                result.Skip(mech, "no stock loadout data for this variant");
            }
            else
            {
                action(mech, stock);
                mech.MarkModified();
                result.Succeed(mech);
            }
        }

        if (result.Succeeded.Count > 0)
        {
            _changed();
        }

        return result;
    }

    private static void MarkHealthy(PropertyList list)
    {
        foreach (var property in list)
        {
            switch (property)
            {
                case EnumProperty { EnumType.Value: "EMWHealthState" } health:
                    health.Value = "EMWHealthState::Healthy";
                    break;
                case StructProperty { Value: PropertyList nested }:
                    MarkHealthy(nested);
                    break;
                case ArrayProperty { Storage: StructArrayStorage array }:
                    foreach (var element in array.PropertyElements)
                    {
                        MarkHealthy(element);
                    }

                    break;
            }
        }
    }

    /// <summary>Reuses the default skin another mech of the same chassis already wears, else derives it.</summary>
    private string SkinFor(MechVariant variant)
    {
        var known = _mechs
            .Where(m => string.Equals(m.Variant?.Chassis, variant.Chassis, StringComparison.Ordinal))
            .Select(m => m.Skin)
            .FirstOrDefault(s => s?.EndsWith("_Default_SKN", StringComparison.OrdinalIgnoreCase) == true);
        return known ?? $"{variant.Chassis.Replace(" ", string.Empty, StringComparison.Ordinal)}_Default_SKN";
    }
}

public sealed class MechRecord
{
    private const string WrapperClass = "/Script/MechWarrior.MWMechLoadoutWrapper";

    private readonly ArrayProperty _byteData;
    private bool _modified;

    private MechRecord(PropertyList wrapper, ArrayProperty byteData, PropertyList blob, byte[] trailing, bool editable)
    {
        Wrapper = wrapper;
        _byteData = byteData;
        Blob = blob;
        Trailing = trailing;
        IsEditable = editable;
    }

    public Guid ItemId { get; private set; }

    public string Asset { get; private set; } = string.Empty;

    public MechVariant? Variant { get; private set; }

    public string Code => Variant?.Code ?? Asset.Replace("_MDA", string.Empty, StringComparison.Ordinal);

    public string? CustomName { get; private set; }

    public string? Skin { get; private set; }

    public MechLocation Location { get; private set; }

    /// <summary>False when the embedded loadout did not round-trip; such mechs are shown but never rewritten.</summary>
    public bool IsEditable { get; }

    public bool IsNew { get; private set; }

    internal PropertyList Wrapper { get; }

    internal PropertyList Blob { get; }

    internal byte[] Trailing { get; }

    internal PropertyList LoadoutData => Blob.GetStruct("MarketItemMech", "Item", "ItemData");

    internal static MechRecord? TryRead(PropertyList wrapper, IReadOnlySet<Guid> bayIds, MechCatalog catalog)
    {
        if (wrapper.Find<ObjectProperty>("ActorClass")?.Path.Value != WrapperClass
            || wrapper.Find<ArrayProperty>("ByteData") is not { Storage: ByteArrayStorage bytes } byteData)
        {
            return null;
        }

        PropertyList blob;
        byte[] trailing;
        bool editable;
        try
        {
            blob = PropertySerializer.ReadList(bytes.Data, out trailing);
            editable = PropertySerializer.WriteList(blob, trailing).AsSpan().SequenceEqual(bytes.Data);
        }
        catch (SaveFormatException)
        {
            return null;
        }

        var record = new MechRecord(wrapper, byteData, blob, trailing, editable);
        return record.Describe(bayIds, catalog) ? record : null;
    }

    internal static MechRecord Create(PropertyList wrapper, PropertyList blob, byte[] trailing, MechVariant? variant)
    {
        var byteData = wrapper.Get<ArrayProperty>("ByteData");
        var record = new MechRecord(wrapper, byteData, blob, (byte[])trailing.Clone(), editable: true)
        {
            IsNew = true,
        };
        record.Describe(new HashSet<Guid>(), catalog: null, variant);
        record.MarkModified();
        record.Commit();
        return record;
    }

    internal void MarkModified() => _modified = true;

    internal void Commit()
    {
        if (!_modified)
        {
            return;
        }

        ((ByteArrayStorage)_byteData.Storage).Data = PropertySerializer.WriteList(Blob, Trailing);
        _modified = false;
    }

    private bool Describe(IReadOnlySet<Guid> bayIds, MechCatalog? catalog, MechVariant? variant = null)
    {
        if (Blob.Find<StructProperty>("MarketItemMech")?.Value is not PropertyList market
            || market.Find<StructProperty>("Item")?.Value is not PropertyList item
            || item.Find<StructProperty>("ItemData")?.Value is not PropertyList data)
        {
            return false;
        }

        ItemId = Ue.ItemGuid(item.Find<StructProperty>("ItemId")?.Value as PropertyList) ?? Guid.Empty;
        Asset = Ue.AssetName(data.Find<StructProperty>("MechDataAssetId")?.Value as PropertyList) ?? "Unknown";
        Variant = variant ?? catalog?.FindByAsset(Asset);
        CustomName = data.Find<StrProperty>("CustomName")?.Value.Value;
        Skin = (data.Find<StructProperty>("SkinCustomization")?.Value as PropertyList)
            ?.Find<StructProperty>("MechSkinAssetId")?.Value is PropertyList skin ? Ue.AssetName(skin) : null;

        var type = Blob.Find<EnumProperty>("MechLoadoutType");
        Location = type is null
            ? bayIds.Contains(ItemId) ? MechLocation.MechBay : MechLocation.Unassigned
            : Ue.EnumValue(type.Value) switch
            {
                "ColdStorageLoadout" => MechLocation.ColdStorage,
                "MarketLoadout" => MechLocation.Market,
                _ => bayIds.Contains(ItemId) ? MechLocation.MechBay : MechLocation.Unassigned,
            };
        return true;
    }
}
