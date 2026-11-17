using MechWarrior5.Clans.SaveEditor.Core.Catalog;

namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>One asset a Clans save uses that the base game doesn't have, and the installed mod behind it, if any.</summary>
public sealed record ClansCompatibilityLine(bool Problem, string Asset, string? Mod, string? Version, string? Url, string Reason);

/// <summary>
/// Read-only: every asset a Clans save uses ('Mechs, omnipods, equipment, inventory) that isn't in the base game, traced
/// to the installed Clans mod that ships it and whether that mod is on. Nothing here changes the save.
/// </summary>
public static class ClansCompatibility
{
    public static IReadOnlyList<ClansCompatibilityLine> Build(ClansDocument doc, ClansGameIndex index)
    {
        var used = doc.Inventory.Actors.Select(a => a.AssetName)
            .Concat(doc.Inventory.Mechs.SelectMany(m => new[] { m.Variant }.Concat(m.Omnipods).Concat(m.Equipment)))
            .Where(n => n.Length > 0 && n != "None")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => !index.BaseAssets.Contains(n))
            .Order(StringComparer.OrdinalIgnoreCase);
        return [.. used.Select(asset => index.Mods.LastOrDefault(m => m.Assets.Contains(asset)) is { } mod
            ? new ClansCompatibilityLine(!mod.Enabled, asset, mod.Name, mod.Version, mod.Url,
                mod.Enabled ? "from this mod" : "this mod is installed but turned off")
            : new ClansCompatibilityLine(true, asset, null, null, null, "not in the game or any installed mod: from a mod that isn't installed"))
            .OrderByDescending(l => l.Problem)];
    }
}
