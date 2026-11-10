using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

/// <summary>A portable loadout (<c>.mw5loadout</c>): variant plus weapons, groups, equipment and armor.</summary>
public sealed record LoadoutFile(string Format, int Version, string Variant, StockLoadout Loadout)
{
    public const string FormatId = "mw5se-loadout";
    public const string Extension = ".mw5loadout";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static LoadoutFile Create(string variantAsset, StockLoadout loadout) => new(FormatId, 1, variantAsset, loadout);

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));

    public static LoadoutFile Load(string path)
    {
        var file = JsonSerializer.Deserialize<LoadoutFile>(File.ReadAllText(path), Json);
        if (file is null || file.Format != FormatId || Checked(file.Loadout) is not { } loadout)
        {
            throw new InvalidDataException("This is not an MW5 Save Editor loadout file.");
        }

        return file with { Loadout = loadout };
    }

    /// <summary>
    /// The loadout with every list present, or null for a damaged or hand-made file missing one (it would fail
    /// partway through applying). Armor maxima may be absent: they're optional and default to none.
    /// </summary>
    internal static StockLoadout? Checked(StockLoadout? loadout) =>
        loadout is { Armor: not null, RearArmor: not null, Structure: not null, Weapons: not null, Groups: not null, Equipment: not null }
        && loadout.Weapons.All(w => w is not null) && loadout.Groups.All(g => g?.G is not null) && loadout.Equipment.All(p => p?.Items is not null)
            ? loadout with { MaxArmor = loadout.MaxArmor ?? [] }
            : null;
}
