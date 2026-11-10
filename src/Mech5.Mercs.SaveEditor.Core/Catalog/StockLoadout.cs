namespace Mech5.Mercs.SaveEditor.Core.Catalog;

/// <summary>A variant's factory loadout as the game defines it.</summary>
public sealed record StockLoadout(
    Dictionary<string, float> Armor,
    Dictionary<string, float> MaxArmor,
    Dictionary<string, float> RearArmor,
    Dictionary<string, float> Structure,
    List<StockWeapon> Weapons,
    List<StockWeaponGroup> Groups,
    List<StockPartEquipment> Equipment);

/// <summary>A weapon hardpoint; melee actuators carry type/name <c>None</c> and are not installed weapons.</summary>
public sealed record StockWeapon(string Slot, string Type, string Name)
{
    public bool IsInstalledWeapon => !string.Equals(Type, "None", StringComparison.Ordinal);
}

public sealed record StockWeaponGroup(string Slot, bool[] G);

public sealed record StockPartEquipment(string Part, List<StockEquipment> Items);

public sealed record StockEquipment(string Type, string Name, int SlotId, string SlotType);
