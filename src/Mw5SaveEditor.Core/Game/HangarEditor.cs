using Mw5SaveEditor.Core.Catalog;
using Mw5SaveEditor.Core.Saves;
using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Game;

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
                var blob = MechLoadoutBuilder.BuildColdStorageBlob(variant.Asset, SkinFor(variant), stock, Guid.NewGuid());
                var wrapper = template.Wrapper.DeepClone();
                var record = MechRecord.Create(wrapper, blob, template.Trailing, variant);
                _wrappers.Elements.Add(wrapper);
                _mechs.Add(record);
            }

            result.Succeed(variant);
        }

        if (result.Succeeded.Count > 0)
        {
            _changed();
        }

        return result;
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

    public StockLoadout ReadLoadout(MechRecord mech) => MechLoadoutReader.Read(mech.LoadoutData);

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

        foreach (var field in MechLoadoutBuilder.BuildStockFields(loadout))
        {
            mech.LoadoutData.Set(field);
        }

        mech.MarkModified();
        _changed();
    }

    internal void Commit()
    {
        foreach (var mech in _mechs)
        {
            mech.Commit();
        }
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

    internal static MechRecord Create(PropertyList wrapper, PropertyList blob, byte[] trailing, MechVariant variant)
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
