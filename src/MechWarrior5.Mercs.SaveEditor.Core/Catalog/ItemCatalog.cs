using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;

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
    /// <summary>The item's own display name (from a mod's asset or an add-on pack); null means label it from the asset id.</summary>
    public string? Label { get; init; }

    /// <summary>The mod or add-on pack the item comes from; null for the base game.</summary>
    public string? Source { get; init; }

    /// <summary>
    /// The slot types the asset itself lists (<c>SupportedSlotTypesAssetId</c>, read for mod equipment and ammo);
    /// null means the built-in slot rules apply. An empty list keeps the item out of every Mech Lab slot.
    /// </summary>
    public IReadOnlyList<string>? SlotTypes { get; init; }

    /// <summary>The ammo tag (<c>Ammo.Missile.LRM</c>): a weapon uses the ammo with the same tag. Null for energy weapons.</summary>
    public string? AmmoType { get; init; }

    /// <summary>Weapons go to <c>WeaponInventory</c>; equipment and ammo to <c>EquipmentInventory</c>.</summary>
    public bool IsWeapon => Kind == ItemKind.Weapon;

    public int Tier
    {
        get
        {
            var at = Asset.LastIndexOf("_Lvl", StringComparison.Ordinal);
            return at >= 0 && int.TryParse(Asset.AsSpan(at + 4), out var tier) ? tier : 0;
        }
    }
}

/// <summary>
/// The built-in weapons, equipment and ammo, then what the installed game's enabled mods add, then what active add-on
/// packs add or change. Each <see cref="Apply"/> rebuilds from the built-in list, so nothing outlives its source.
/// </summary>
public sealed class ItemCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyList<ItemDefinition> _builtIn;
    private Dictionary<string, ItemDefinition> _byAsset;

    private ItemCatalog(IReadOnlyList<ItemDefinition> items)
    {
        _builtIn = items;
        Items = items;
        _byAsset = Index(items);
    }

    public IReadOnlyList<ItemDefinition> Items { get; private set; }

    /// <summary>
    /// Weapons for a hardpoint. A mod weapon whose hardpoint class couldn't be read is left out rather than guessed
    /// (it is still in <see cref="Items"/>, so the inventory can stock it).
    /// </summary>
    public IEnumerable<ItemDefinition> WeaponsFor(HardpointClass slot) =>
        Items.Where(i => i.Kind == ItemKind.Weapon && (i.Source is null || i.Hardpoint != HardpointClass.Unknown) && Hardpoints.Accepts(slot, i.Hardpoint));

    public ItemDefinition? Find(string asset) => _byAsset.GetValueOrDefault(asset);

    public bool IsBuiltIn(string asset) => _builtIn.Any(i => string.Equals(i.Asset, asset, StringComparison.OrdinalIgnoreCase));

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

    /// <summary>Built-in items, plus the enabled mods' items, plus the active packs' additions and changes.</summary>
    public void Apply(GameData? game, AddonState addons)
    {
        // The game's own data first (ammo tags included), then the packs, which may correct any of it.
        var tags = game?.AmmoTypes ?? new Dictionary<string, string>();
        ItemDefinition Tagged(ItemDefinition i) => tags.TryGetValue(i.Asset, out var tag) ? i with { AmmoType = tag } : i;
        var items = _builtIn.Select(Tagged).ToDictionary(i => i.Asset, StringComparer.OrdinalIgnoreCase);
        var order = _builtIn.Select(i => i.Asset).ToList();
        foreach (var item in (game?.Items ?? []).Select(Tagged))
        {
            if (items.TryAdd(item.Asset, item))
            {
                order.Add(item.Asset);
            }
        }

        foreach (var pack in addons.Active)
        {
            foreach (var add in pack.Items.Add)
            {
                if (items.TryGetValue(add.Asset, out var known))
                {
                    items[add.Asset] = Override(known, add, pack.Name);
                }
                else if (FromPack(add, pack.Name) is { } item)
                {
                    items[add.Asset] = item;
                    order.Add(add.Asset);
                }
            }

            foreach (var change in pack.Items.Override.Where(c => items.ContainsKey(c.Asset)))
            {
                items[change.Asset] = Override(items[change.Asset], change, pack.Name);
            }
        }

        Items = [.. order.Select(a => items[a])];
        _byAsset = Index(Items);
    }

    /// <summary>
    /// The ammo a weapon fires, from the game's own tags: a full ton first, then half tons, matching the weapon's mod when
    /// it has one. Empty for weapons without ammo, or before the game data is read.
    /// </summary>
    public IReadOnlyList<ItemDefinition> AmmoFor(ItemDefinition weapon) =>
        weapon.AmmoType is not { } tag
            ? []
            : [.. Items.Where(i => i.Kind == ItemKind.Ammo && string.Equals(i.AmmoType, tag, StringComparison.Ordinal))
                .OrderBy(i => i.Asset.Contains("_Half", StringComparison.OrdinalIgnoreCase))
                .ThenBy(i => !string.Equals(i.Source, weapon.Source, StringComparison.Ordinal))
                .ThenBy(i => i.Asset, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Back to the built-in items alone.</summary>
    public void Reset()
    {
        Items = _builtIn;
        _byAsset = Index(_builtIn);
    }

    private static ItemDefinition Override(ItemDefinition item, AddonItem change, string pack) => item with
    {
        AssetType = change.Type ?? item.AssetType,
        Kind = Kind(change.Kind) ?? item.Kind,
        Hardpoint = change.Hardpoint is { } h ? Hardpoints.Parse(h) : item.Hardpoint,
        Label = change.Label ?? item.Label,
        AmmoType = change.AmmoType ?? item.AmmoType,
        Source = item.Source ?? pack,
    };

    private static ItemDefinition? FromPack(AddonItem add, string pack) =>
        add.Type is { } type && Kind(add.Kind) is { } kind
            ? new ItemDefinition(add.Asset, type, kind, Hardpoints.Parse(add.Hardpoint)) { Label = add.Label, Source = pack, AmmoType = add.AmmoType }
            : null;

    private static ItemKind? Kind(string? kind) => kind?.ToLowerInvariant() switch
    {
        "weapon" => ItemKind.Weapon,
        "equipment" => ItemKind.Equipment,
        "ammo" => ItemKind.Ammo,
        _ => null,
    };

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
