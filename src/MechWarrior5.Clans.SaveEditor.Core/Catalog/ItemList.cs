using System.Text.Json;
using MechWarrior5.Clans.SaveEditor.Core.Game;

namespace MechWarrior5.Clans.SaveEditor.Core.Catalog;

/// <summary>A stackable item the editor can add: its asset type and asset name.</summary>
public sealed record KnownItem(string Type, string Asset)
{
    public string Label => ClansCatalog.ItemLabel(Asset);
}

/// <summary>
/// Stackable assets the editor knows: a built-in list gathered from real campaign saves, plus
/// anything in the open save (so modded or newer items are always offered).
/// </summary>
public static class ItemList
{
    private static readonly Lazy<IReadOnlyList<KnownItem>> BuiltIn = new(LoadBuiltIn);

    public static IReadOnlyList<KnownItem> For(InventoryEditor inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return [.. BuiltIn.Value
            .Concat(inventory.Stacks.Select(s => new KnownItem(s.AssetType, s.AssetName)))
            .Where(i => i.Asset.Length > 0)
            .DistinctBy(i => i.Asset)
            .OrderBy(i => i.Label, StringComparer.CurrentCulture)];
    }

    private static IReadOnlyList<KnownItem> LoadBuiltIn()
    {
        using var stream = typeof(ItemList).Assembly.GetManifestResourceStream("MechWarrior5.Clans.SaveEditor.Core.Data.items.json");
        if (stream is null)
        {
            return [];
        }

        using var doc = JsonDocument.Parse(stream);
        return [.. doc.RootElement.EnumerateArray()
            .Select(e => new KnownItem(e.GetProperty("type").GetString() ?? string.Empty, e.GetProperty("asset").GetString() ?? string.Empty))];
    }
}
