using System.IO.Compression;
using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

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
    private readonly HashSet<string> _builtInAssets;

    private MechCatalog(IReadOnlyList<DlcPack> packs, IReadOnlyList<MechVariant> variants)
    {
        DlcPacks = packs;
        _variants = [.. variants];
        _byAsset = variants.ToDictionary(v => v.Asset, StringComparer.OrdinalIgnoreCase);
        _builtInAssets = variants.Select(v => v.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _stock = new Lazy<Dictionary<string, StockLoadout>>(LoadStock);
    }

    public IReadOnlyList<DlcPack> DlcPacks { get; }

    public IReadOnlyList<MechVariant> Variants => _variants;

    /// <summary>The installed game the catalog was refreshed from, if any.</summary>
    public GameData? Game { get; private set; }

    /// <summary>The highest pilot skill cap: the installed game's (and its mods') skill level curve, else 10.</summary>
    public int MaxSkillCap => Game?.MaxSkillCap ?? Pilot.DefaultMaxSkillCap;

    /// <summary>
    /// Folds in what the installed game and its enabled mods define: new and modded variants, DLC
    /// membership from the game's own folders, and the game's stock loadouts (which win over the
    /// built-in ones).
    /// </summary>
    /// <summary>Drops what an earlier scan added (mod variants, the game's loadouts), back to the built-in data alone.</summary>
    public void Reset()
    {
        _gameStock.Clear();
        foreach (var added in _variants.Where(v => !_builtInAssets.Contains(v.Asset)).ToList())
        {
            _variants.Remove(added);
            _byAsset.Remove(added.Asset);
        }

        Game = null;
    }

    public void Apply(GameData game)
    {
        // A rescan replaces the last one: a mod disabled since then no longer offers its mechs or loadouts.
        _gameStock.Clear();
        var scannedAssets = game.Mechs.Select(m => m.Variant.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _variants.Where(v => !_builtInAssets.Contains(v.Asset) && !scannedAssets.Contains(v.Asset)).ToList())
        {
            _variants.Remove(stale);
            _byAsset.Remove(stale.Asset);
        }

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
        _gameStock.GetValueOrDefault(asset) is { } game
            ? WithFullStructure(WithMaxArmor(game, _stock.Value.GetValueOrDefault(asset)), _stock.Value.GetValueOrDefault(asset))
            : _stock.Value.GetValueOrDefault(asset);

    /// <summary>Game loadout assets carry no armor maxima; keep the built-in ones so armor limits still apply.</summary>
    private static StockLoadout WithMaxArmor(StockLoadout game, StockLoadout? builtIn) =>
        game.MaxArmor.Count > 0 || builtIn is null ? game : game with { MaxArmor = builtIn.MaxArmor };

    /// <summary>
    /// Some game loadout assets store damaged structure (a 0 arm on the BLR-1G); a repaired or stock mech needs each
    /// part's full structure: the higher of the game and built-in values, or for a mech only the game defines (mods)
    /// the higher of each left/right pair (built-in data keeps real asymmetries such as the RFL-LK's arms).
    /// </summary>
    private static StockLoadout WithFullStructure(StockLoadout game, StockLoadout? builtIn)
    {
        var full = new Dictionary<string, float>(game.Structure, StringComparer.Ordinal);
        if (builtIn is not null)
        {
            foreach (var (part, value) in builtIn.Structure)
            {
                full[part] = Math.Max(value, full.GetValueOrDefault(part));
            }

            return game with { Structure = full };
        }

        foreach (var side in new[] { "Arm", "Torso", "Leg" })
        {
            if (full.TryGetValue("Left" + side, out var left) && full.TryGetValue("Right" + side, out var right))
            {
                full["Left" + side] = full["Right" + side] = Math.Max(left, right);
            }
        }

        return game with { Structure = full };
    }

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
