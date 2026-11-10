using System.Text.Json;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

public enum ItemKind
{
    Weapon,
    Equipment,
    Ammo,
}

/// <summary>An inventory item asset, e.g. <c>MediumLaser_Lvl2</c> of type <c>MWTraceWeaponDataAsset</c>.</summary>
public sealed record ItemDefinition(string Asset, string AssetType, ItemKind Kind, HardpointClass Hardpoint = HardpointClass.Unknown)
{
    /// <summary>Weapons go to <c>WeaponInventory</c>; equipment and ammo to <c>EquipmentInventory</c>.</summary>
    public bool IsWeapon => Kind == ItemKind.Weapon;

    /// <summary>The asset's own display name (mod items); null means label it from the asset id.</summary>
    public string? Label { get; init; }

    /// <summary>Set for items an enabled mod adds.</summary>
    public string? ModName { get; init; }

    /// <summary>
    /// The slot types the asset itself lists (<c>SupportedSlotTypesAssetId</c>, read for mod equipment and ammo);
    /// null means the built-in slot rules apply. An empty list keeps the item out of every Mech Lab slot.
    /// </summary>
    public IReadOnlyList<string>? SlotTypes { get; init; }

    public int Tier
    {
        get
        {
            var at = Asset.LastIndexOf("_Lvl", StringComparison.Ordinal);
            return at >= 0 && int.TryParse(Asset.AsSpan(at + 4), out var tier) ? tier : 0;
        }
    }
}

/// <summary>The built-in weapons, equipment and ammo, plus what the installed game's enabled mods add.</summary>
public sealed class ItemCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyList<ItemDefinition> _builtIn;
    private readonly HashSet<string> _builtInAssets;
    private Dictionary<string, ItemDefinition> _byAsset;

    private ItemCatalog(IReadOnlyList<ItemDefinition> items)
    {
        _builtIn = items;
        _builtInAssets = items.Select(i => i.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Items = items;
        _byAsset = Index(items);
    }

    public IReadOnlyList<ItemDefinition> Items { get; private set; }

    /// <summary>
    /// Weapons for a hardpoint. A mod weapon whose hardpoint class couldn't be read is left out rather than guessed
    /// (it is still in <see cref="Items"/>, so the inventory can stock it).
    /// </summary>
    public IEnumerable<ItemDefinition> WeaponsFor(HardpointClass slot) =>
        Items.Where(i => i.Kind == ItemKind.Weapon && i.Hardpoint != HardpointClass.Unknown && Hardpoints.Accepts(slot, i.Hardpoint));

    public ItemDefinition? Find(string asset) => _byAsset.GetValueOrDefault(asset);

    /// <summary>
    /// Adds the items the scanned game's enabled mods define. A rescan replaces the last one, so a mod disabled since
    /// then no longer offers its items; built-in entries always stay (a mod's copy of a base asset doesn't replace it).
    /// </summary>
    public void Apply(GameData game) =>
        SetItems([.. _builtIn, .. game.Items.Where(i => !_builtInAssets.Contains(i.Asset))]);

    /// <summary>Back to the built-in items alone.</summary>
    public void Reset() => SetItems(_builtIn);

    public static ItemCatalog LoadBuiltIn()
    {
        using var stream = MechCatalog.OpenResource("items.json");
        var file = JsonSerializer.Deserialize<Dictionary<string, List<ItemDto>>>(stream, JsonOptions)
            ?? throw new InvalidDataException("items.json is empty");
        var items = new List<ItemDefinition>();
        Add("weapons", ItemKind.Weapon);
        Add("equipment", ItemKind.Equipment);
        Add("ammo", ItemKind.Ammo);
        return new ItemCatalog(items);

        void Add(string group, ItemKind kind) =>
            items.AddRange(file[group].Select(i => new ItemDefinition(i.Asset, i.Type, kind, Hardpoints.Parse(i.Hardpoint))));
    }

    private void SetItems(IReadOnlyList<ItemDefinition> items)
    {
        Items = items;
        _byAsset = Index(items);
    }

    private static Dictionary<string, ItemDefinition> Index(IEnumerable<ItemDefinition> items)
    {
        var index = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            index.TryAdd(item.Asset, item);
        }

        return index;
    }

    private sealed record ItemDto(string Asset, string Type, string? Hardpoint);
}
