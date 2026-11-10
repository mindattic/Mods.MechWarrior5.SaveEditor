namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>A mech in the Clans inventory, viewed through its <c>MechLoadout</c>.</summary>
public sealed class ClansMech
{
    private static readonly string[] ArmorPairs = ["Armor", "RearArmor"];

    internal ClansMech(InventoryActor actor)
    {
        Actor = actor;
    }

    public InventoryActor Actor { get; }

    public Guid? InventoryId => Actor.InventoryId;

    /// <summary>The mech item asset, e.g. <c>VPR_MIA</c> (Viper, Prime configuration via its omnipods).</summary>
    public string Variant => Ue.AssetName(Loadout.Find<StructProperty>("MechItemDataAssetId")?.Value as PropertyList)
        ?? Actor.AssetName;

    /// <summary>The chassis code, e.g. <c>VPR</c>.</summary>
    public string Chassis => Variant.Split('_')[0];

    public string CustomName
    {
        get => Loadout.Find<StrProperty>("CustomName")?.Value.Value is { } name && name != "DEFAULT" ? name : string.Empty;
        set
        {
            var text = string.IsNullOrWhiteSpace(value) ? "DEFAULT" : value.Trim();
            if (Loadout.Find<StrProperty>("CustomName") is { } existing)
            {
                existing.Value = FString.From(text);
            }
            else
            {
                Loadout.Add(new StrProperty("CustomName", text) { Ue5 = Ue5Tag.Simple("StrProperty") });
            }
        }
    }

    /// <summary>Omnipod asset names per location, e.g. <c>VPR-PRIME_LA_Omnipod</c>.</summary>
    public IReadOnlyList<string> Omnipods =>
        Loadout.Find<ArrayProperty>("EquippedOmnipods")?.Storage is StructArrayStorage pods
            ? [.. pods.PropertyElements.Select(p => Ue.AssetName(p) ?? string.Empty)]
            : [];

    /// <summary>Installed weapons and equipment by asset name (empty slots skipped).</summary>
    public IReadOnlyList<string> Equipment => [.. EquippedSlots()
        .Select(slot => Ue.AssetName(ItemInstance(slot)?.Find<StructProperty>("AssetId")?.Value as PropertyList))
        .Where(name => name is not null and not "None")
        .Select(name => name!)];

    /// <summary>
    /// True when any armor is below its installed value, any part is not healthy, or a location is destroyed (it has
    /// armor installed but no structure entry: Unreal omits a 0 value).
    /// </summary>
    public bool IsDamaged =>
        ArmorPairs.Any(pair => Locations(pair).Any(l => l.Current < l.Installed))
        || EquippedSlots().Any(slot => slot.Find<EnumProperty>("HealthState") is { } h && Ue.EnumValue(h.Value) != "Healthy")
        || DestroyedLocations().Any();

    /// <summary>Locations with armor installed whose structure is missing (destroyed); none when the save keeps no structure.</summary>
    private IEnumerable<string> DestroyedLocations()
    {
        if (Loadout.Find<StructProperty>("CurrentStructure")?.Value is not PropertyList structure
            || Loadout.Find<StructProperty>("InstalledArmor")?.Value is not PropertyList installed)
        {
            return [];
        }

        return installed.OfType<FloatProperty>()
            .Where(a => a.Value > 0 && (structure.Find<FloatProperty>(a.Name)?.Value ?? 0) <= 0)
            .Select(a => a.Name);
    }

    public float TotalArmor => Locations("Armor").Sum(l => l.Installed) + Locations("RearArmor").Sum(l => l.Installed);

    internal PropertyList Loadout => Actor.Properties.Get<StructProperty>("Loadout").Properties;

    /// <summary>Internal structure per location (<c>CurrentStructure</c>); the save keeps no maximum.</summary>
    public IReadOnlyDictionary<string, float> Structure =>
        (Loadout.Find<StructProperty>("CurrentStructure")?.Value as PropertyList)?.OfType<FloatProperty>().ToDictionary(f => f.Name, f => f.Value)
        ?? [];

    /// <summary>
    /// Restores armor to its installed values, every installed part to Healthy, and internal structure up to
    /// <paramref name="fullStructure"/> (the save keeps no structure maximum, so the caller supplies one).
    /// </summary>
    public bool Repair(IReadOnlyDictionary<string, float>? fullStructure = null)
    {
        var changed = false;
        if (fullStructure is not null && Loadout.Find<StructProperty>("CurrentStructure")?.Value is PropertyList structure)
        {
            foreach (var part in structure.OfType<FloatProperty>())
            {
                if (fullStructure.TryGetValue(part.Name, out var full) && part.Value < full)
                {
                    part.Value = full;
                    changed = true;
                }
            }

            // Unreal leaves a 0 value out of the save, so a destroyed location (an arm shot off) has no entry at all:
            // put it back at full structure too.
            foreach (var (name, full) in fullStructure)
            {
                if (full > 0 && structure.Find<FloatProperty>(name) is null)
                {
                    structure.Add(new FloatProperty(name, full) { Ue5 = Ue5Tag.Simple("FloatProperty") });
                    changed = true;
                }
            }
        }

        foreach (var pair in ArmorPairs)
        {
            foreach (var location in Locations(pair).Where(l => l.Current < l.Installed))
            {
                location.SetCurrent(location.Installed);
                changed = true;
            }
        }

        foreach (var slot in EquippedSlots())
        {
            if (slot.Find<EnumProperty>("HealthState") is { } health && Ue.EnumValue(health.Value) != "Healthy")
            {
                health.Value = FString.From("EMWHealthState::Healthy");
                changed = true;
            }
        }

        return changed;
    }

    private IEnumerable<PropertyList> EquippedSlots()
    {
        if (Loadout.Find<ArrayProperty>("InstalledEquipment")?.Storage is not StructArrayStorage lists)
        {
            yield break;
        }

        foreach (var list in lists.PropertyElements)
        {
            if (list.Find<ArrayProperty>("EquippedItemArray")?.Storage is StructArrayStorage slots)
            {
                foreach (var slot in slots.PropertyElements)
                {
                    yield return slot;
                }
            }
        }
    }

    private static PropertyList? ItemInstance(PropertyList slot) => slot.Find<StructProperty>("ItemInstance")?.Value as PropertyList;

    /// <summary>Pairs <c>CurrentX.Location</c> with <c>InstalledX.Location</c> (X = Armor or RearArmor).</summary>
    private IEnumerable<ArmorLocation> Locations(string suffix)
    {
        var current = Loadout.Find<StructProperty>("Current" + suffix)?.Value as PropertyList;
        if (Loadout.Find<StructProperty>("Installed" + suffix)?.Value is not PropertyList installed || current is null)
        {
            yield break;
        }

        foreach (var value in installed.OfType<FloatProperty>())
        {
            yield return new ArmorLocation(value.Name, value, current);
        }
    }

    private sealed class ArmorLocation(string name, FloatProperty installed, PropertyList current)
    {
        public float Installed => installed.Value;

        public float Current => current.Find<FloatProperty>(name)?.Value ?? 0;

        public void SetCurrent(float value)
        {
            if (current.Find<FloatProperty>(name) is { } existing)
            {
                existing.Value = value;
            }
            else
            {
                current.Add(new FloatProperty(name, value) { Ue5 = Ue5Tag.Simple("FloatProperty") });
            }
        }
    }
}
