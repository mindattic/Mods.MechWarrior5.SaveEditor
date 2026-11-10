using System.Text.Json;
using Mw5SaveEditor.Core.Catalog;

namespace Mw5SaveEditor.Core.Game;

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
        if (file is null || file.Format != FormatId || file.Loadout is null)
        {
            throw new InvalidDataException("This is not an MW5 Save Editor loadout file.");
        }

        return file;
    }
}
