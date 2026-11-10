using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

public sealed record TransferMech(string Asset, string? Skin, StockLoadout Loadout);

/// <summary>A pilot as its serialized roster entry (base64), so every field travels intact.</summary>
public sealed record TransferPilot(string Name, string Data);

public sealed record TransferItem(string Asset, string AssetType, bool IsWeapon, bool IsAmmo, int Count);

/// <summary>A company exported from one campaign (<c>.mw5company</c>) for import into another.</summary>
public sealed record CompanyPackage(
    string Format,
    int Version,
    string Company,
    string? GameVersion,
    List<TransferMech> Mechs,
    List<TransferPilot> Pilots,
    List<TransferItem> Inventory)
{
    public const string FormatId = "mw5se-company";
    public const string Extension = ".mw5company";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));

    public static CompanyPackage Load(string path)
    {
        var package = JsonSerializer.Deserialize<CompanyPackage>(File.ReadAllText(path), Json);
        if (package is null || package.Format != FormatId || package.Mechs is null || package.Pilots is null || package.Inventory is null
            || package.Mechs.Any(m => m is null || LoadoutFile.Checked(m.Loadout) is null) || package.Pilots.Any(p => p is null) || package.Inventory.Any(i => i is null))
        {
            throw new InvalidDataException("This is not an MW5 Save Editor company file.");
        }

        return package with { Mechs = [.. package.Mechs.Select(m => m with { Loadout = LoadoutFile.Checked(m.Loadout)! })] };
    }
}

public sealed record TransferOptions(bool Mechs = true, bool Pilots = true, bool Inventory = true);

public sealed class TransferReport
{
    public BatchResult<string> Mechs { get; } = new();

    public BatchResult<string> Pilots { get; } = new();

    public BatchResult<string> Inventory { get; } = new();
}

/// <summary>Moves a company's mechs, MechWarriors and stock between campaigns.</summary>
public static class CompanyTransfer
{
    /// <summary>Exports every owned mech (bay and cold storage), every pilot but the commander, and all healthy stock.</summary>
    public static CompanyPackage Export(Mw5Document doc) => new(
        CompanyPackage.FormatId,
        1,
        doc.Company.CompanyName,
        doc.File.ProjectVersion,
        [.. doc.Hangar.Mechs
            .Where(m => m.Location is MechLocation.MechBay or MechLocation.ColdStorage && m.IsEditable)
            .Select(m => new TransferMech(m.Asset, m.Skin, HangarEditor.ReadLoadout(m)))],
        [.. doc.Roster.Pilots
            .Where(p => !p.IsPlayer)
            .Select(p => new TransferPilot(p.DisplayName, Convert.ToBase64String(PropertySerializer.WriteList(p.Element))))],
        [.. doc.Inventory.Items
            .Where(i => i.IsHealthy)
            .Select(i => new TransferItem(i.Asset, i.AssetType, i.IsWeapon, i.IsAmmo, i.Count))]);

    /// <summary>Imports a package: mechs arrive in cold storage, pilots join the roster, stock is added.</summary>
    public static TransferReport Import(Mw5Document doc, CompanyPackage package, MechCatalog catalog, TransferOptions options)
    {
        var report = new TransferReport();
        if (options.Mechs)
        {
            foreach (var mech in package.Mechs)
            {
                var label = catalog.FindByAsset(mech.Asset)?.DisplayName ?? mech.Asset;
                // The package carries the mech's damaged structure but full armor and healthy gear: it arrives
                // fully repaired (as the stock does) rather than half-repaired, when the full structure is known.
                var loadout = catalog.FindStock(mech.Asset) is { Structure.Count: > 0 } stock
                    ? mech.Loadout with
                    {
                        Structure = stock.Structure.Keys.Union(mech.Loadout.Structure.Keys)
                            .ToDictionary(p => p, p => Math.Max(stock.Structure.GetValueOrDefault(p), mech.Loadout.Structure.GetValueOrDefault(p))),
                    }
                    : mech.Loadout;
                if (doc.Hangar.AddLoadoutToColdStorage(mech.Asset, mech.Skin, loadout) is { } reason)
                {
                    report.Mechs.Skip(label, reason);
                }
                else
                {
                    report.Mechs.Succeed(label);
                }
            }
        }

        if (options.Pilots)
        {
            foreach (var pilot in package.Pilots)
            {
                try
                {
                    var element = PropertySerializer.ReadList(Convert.FromBase64String(pilot.Data), out _);
                    doc.Roster.Import(element);
                    report.Pilots.Succeed(pilot.Name);
                }
                catch (Exception e) when (e is FormatException or SaveFormatException or InvalidOperationException)
                {
                    report.Pilots.Skip(pilot.Name, "its data couldn't be read");
                }
            }
        }

        if (options.Inventory)
        {
            foreach (var group in package.Inventory.GroupBy(i => (i.Asset, i.AssetType, i.IsWeapon, i.IsAmmo)))
            {
                var kind = group.Key.IsWeapon ? ItemKind.Weapon : group.Key.IsAmmo ? ItemKind.Ammo : ItemKind.Equipment;
                var count = Math.Min(group.Sum(i => i.Count), InventoryEditor.MaxCount);
                var result = doc.Inventory.Add([new ItemDefinition(group.Key.Asset, group.Key.AssetType, kind)], count);
                if (result.Skipped.Count > 0)
                {
                    report.Inventory.Skip(group.Key.Asset, result.Skipped[0].Reason);
                }
                else
                {
                    report.Inventory.Succeed($"{group.Key.Asset} x{count}");
                }
            }
        }

        return report;
    }
}
