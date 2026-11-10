using System.IO.Compression;
using System.Text.Json;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

public enum WeightClass
{
    Light,
    Medium,
    Heavy,
    Assault,
}

public sealed record DlcPack(string Tag, string Name)
{
    public string ShortName => Tag.Replace("DLC.", string.Empty, StringComparison.Ordinal);
}

/// <summary>A purchasable 'Mech variant, e.g. <c>AS7-D</c> (asset <c>AS7-D_MDA</c>).</summary>
public sealed record MechVariant(
    string Code,
    string Asset,
    string Chassis,
    string ChassisName,
    string SarnaTitle,
    int Tons,
    WeightClass WeightClass,
    string? HeroName,
    DlcPack? Dlc)
{
    /// <summary>Set for variants an enabled mod adds.</summary>
    public string? ModName { get; init; }

    public string DisplayName => HeroName is null ? $"{ChassisName} {Code}" : $"{HeroName} ({Code})";
}

/// <summary>The built-in mech list, DLC membership and stock loadouts (see THIRD-PARTY-NOTICES.md).</summary>
public sealed class MechCatalog
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, MechVariant> _byAsset;
    private readonly List<MechVariant> _variants;
    private readonly Lazy<Dictionary<string, StockLoadout>> _stock;
    private readonly Dictionary<string, StockLoadout> _gameStock = new(StringComparer.OrdinalIgnoreCase);

    private MechCatalog(IReadOnlyList<DlcPack> packs, IReadOnlyList<MechVariant> variants)
    {
        DlcPacks = packs;
        _variants = [.. variants];
        _byAsset = variants.ToDictionary(v => v.Asset, StringComparer.OrdinalIgnoreCase);
        _stock = new Lazy<Dictionary<string, StockLoadout>>(LoadStock);
    }

    public IReadOnlyList<DlcPack> DlcPacks { get; }

    public IReadOnlyList<MechVariant> Variants => _variants;

    /// <summary>The installed game the catalog was refreshed from, if any.</summary>
    public GameData? Game { get; private set; }

    /// <summary>
    /// Folds in what the installed game and its enabled mods define: new and modded variants, DLC
    /// membership from the game's own folders, and the game's stock loadouts (which win over the
    /// built-in ones).
    /// </summary>
    public void Apply(GameData game)
    {
        foreach (var scanned in game.Mechs)
        {
            if (_byAsset.TryGetValue(scanned.Variant.Asset, out var existing))
            {
                if (existing != scanned.Variant)
                {
                    _variants[_variants.IndexOf(existing)] = scanned.Variant;
                    _byAsset[scanned.Variant.Asset] = scanned.Variant;
                }
            }
            else
            {
                _variants.Add(scanned.Variant);
                _byAsset[scanned.Variant.Asset] = scanned.Variant;
            }

            if (scanned.Loadout is { } loadout)
            {
                _gameStock[scanned.Variant.Asset] = loadout;
            }
        }

        Game = game;
    }

    public static MechCatalog LoadBuiltIn()
    {
        using var stream = OpenResource("mechs.json");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, Json)
            ?? throw new InvalidDataException("mechs.json is empty");
        var packs = file.DlcPacks.Select(p => new DlcPack(p.Tag, p.Name)).ToList();
        var byTag = packs.ToDictionary(p => p.Tag, StringComparer.Ordinal);
        var variants = file.Mechs.Select(m => new MechVariant(
            m.Code,
            m.Asset,
            m.Chassis,
            m.Name,
            m.Sarna,
            m.Tons,
            Enum.Parse<WeightClass>(m.Class),
            m.Hero,
            m.Dlc is null ? null : byTag[m.Dlc])).ToList();
        return new MechCatalog(packs, variants);
    }

    public MechVariant? FindByAsset(string asset) => _byAsset.GetValueOrDefault(asset);

    public StockLoadout? FindStock(string asset) =>
        _gameStock.GetValueOrDefault(asset) is { } game ? WithMaxArmor(game, _stock.Value.GetValueOrDefault(asset)) : _stock.Value.GetValueOrDefault(asset);

    /// <summary>Game loadout assets carry no armor maxima; keep the built-in ones so armor limits still apply.</summary>
    private static StockLoadout WithMaxArmor(StockLoadout game, StockLoadout? builtIn) =>
        game.MaxArmor.Count > 0 || builtIn is null ? game : game with { MaxArmor = builtIn.MaxArmor };

    internal static Stream OpenResource(string fileName) =>
        typeof(MechCatalog).Assembly.GetManifestResourceStream($"MechWarrior5.Mercs.SaveEditor.Core.Data.{fileName}")
        ?? throw new InvalidDataException($"Missing built-in data file {fileName}");

    private static Dictionary<string, StockLoadout> LoadStock()
    {
        using var stream = new GZipStream(OpenResource("stock_loadouts.json.gz"), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<Dictionary<string, StockLoadout>>(stream, Json)
            ?? throw new InvalidDataException("stock_loadouts.json.gz is empty");
    }

    private sealed record CatalogFile(List<PackDto> DlcPacks, List<MechDto> Mechs);

    private sealed record PackDto(string Tag, string Name);

    private sealed record MechDto(
        string Code, string Asset, string Chassis, string Name, string Sarna, int Tons, string Class, string? Hero, string? Dlc);
}
