namespace MechWarrior5.Clans.SaveEditor.Core.Game;

public enum ActorKind
{
    Stackable,
    Mech,
    Pilot,
    Unlock,
    Other,
}

/// <summary>What a stackable item is, for grouping in the UI.</summary>
public enum StackCategory
{
    Currency,
    MechXp,
    Salvage,
    Crew,
    Equipment,
    Other,
}

/// <summary>
/// One entry of the campaign's <c>InventoryData</c>: an <c>ActorSaveData</c> whose <c>ByteData</c>
/// holds the item object (<c>KelStackableInventoryItem</c>, <c>KelMechInventoryItem</c>, ...).
/// The object stream is parsed on load and written back by <see cref="Commit"/>.
/// </summary>
public sealed class InventoryActor
{
    private readonly ByteArrayStorage _bytes;

    internal InventoryActor(PropertyList element)
    {
        Element = element;
        ActorClass = element.Get<ObjectProperty>("ActorClass").Path.ToString();
        _bytes = element.Get<ArrayProperty>("ByteData").Bytes;
        Item = Ue5ObjectBlob.Read(_bytes.Data);
        Kind = ActorClass switch
        {
            _ when ActorClass.EndsWith(".KelStackableInventoryItem", StringComparison.Ordinal) => ActorKind.Stackable,
            _ when ActorClass.EndsWith(".KelMechInventoryItem", StringComparison.Ordinal) => ActorKind.Mech,
            _ when ActorClass.EndsWith(".KelPilotInventoryItem", StringComparison.Ordinal) => ActorKind.Pilot,
            _ when ActorClass.EndsWith(".KelPersistentUnlockItem", StringComparison.Ordinal) => ActorKind.Unlock,
            _ => ActorKind.Other,
        };
    }

    /// <summary>The <c>ActorSaveData</c> struct in the inventory array.</summary>
    public PropertyList Element { get; }

    public string ActorClass { get; }

    public ActorKind Kind { get; }

    /// <summary>The parsed item object stored in <c>ByteData</c>.</summary>
    public Ue5ObjectBlob Item { get; }

    public PropertyList Properties => Item.Properties;

    public PropertyList? ItemData => Properties.Find<StructProperty>("ItemData")?.Value as PropertyList;

    public Guid? InventoryId => Ue.ItemGuid(ItemData?.Find<StructProperty>("InventoryId")?.Value as PropertyList);

    public string AssetType => Ue.AssetType(AssetIdStruct) ?? string.Empty;

    public string AssetName => Ue.AssetName(AssetIdStruct) ?? string.Empty;

    public StackCategory Category => Kind != ActorKind.Stackable
        ? StackCategory.Other
        : AssetType switch
        {
            "KelCrewItemDataAsset" => StackCategory.Crew,
            "KelEquipmentItemDataAsset" => StackCategory.Equipment,
            "KelCurrencyItemDataAsset" when AssetName.StartsWith("MXP_", StringComparison.Ordinal) => StackCategory.MechXp,
            "KelCurrencyItemDataAsset" when AssetName.StartsWith("Salvage_", StringComparison.Ordinal) => StackCategory.Salvage,
            "KelCurrencyItemDataAsset" => StackCategory.Currency,
            _ => StackCategory.Other,
        };

    /// <summary>The stack size; Unreal omits zero, so a missing property reads as 0.</summary>
    public int Quantity
    {
        get => Properties.Find<IntProperty>("Quantity")?.Value ?? 0;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (Properties.Find<IntProperty>("Quantity") is { } quantity)
            {
                quantity.Value = value;
            }
            else
            {
                Properties.Insert(0, new IntProperty("Quantity", value) { Ue5 = Ue5Tag.Simple("IntProperty") });
            }
        }
    }

    private PropertyList? AssetIdStruct => ItemData?.Find<StructProperty>("AssetId")?.Value as PropertyList;

    /// <summary>A deep copy of this actor with a fresh inventory id (the copy is not yet in any inventory).</summary>
    public InventoryActor CloneWithNewId()
    {
        var clone = new InventoryActor(CloneElement());
        if (clone.ItemData?.Find<StructProperty>("InventoryId")?.Value is PropertyList id
            && id.Find<StructProperty>("Value") is { } value)
        {
            value.Value = new GuidValue(Guid.NewGuid());
        }

        return clone;
    }

    /// <summary>Writes the parsed item object back into <c>ByteData</c>.</summary>
    public void Commit() => _bytes.Data = Item.ToArray();

    private PropertyList CloneElement()
    {
        Commit();
        return Element.DeepClone();
    }
}
