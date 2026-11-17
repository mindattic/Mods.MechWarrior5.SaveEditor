using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Shared.Assets;

namespace MechWarrior5.SaveEditor.App.Services;

/// <summary>
/// MechWarrior 5: Clans' own 'Mech pictures (<c>UI/Textures/Battlemechs/*_Icon_UIX</c>), read from its IoStore paks. Every
/// such block is Oodle-compressed, so this works only with an Oodle DLL already on the player's PC (<see cref="OodleLocator"/>).
/// </summary>
public sealed class ClansArt
{
    private readonly Oodle _oodle;
    private readonly Dictionary<string, (IoStoreContainer Container, int Chunk)> _icons;

    private ClansArt(Oodle oodle, Dictionary<string, (IoStoreContainer, int)> icons)
    {
        _oodle = oodle;
        _icons = icons;
    }

    public int Count => _icons.Count;

    /// <summary>
    /// The Honor ladder from the game's own <c>Progression/Honor/HonorLevelingAsset</c> (base campaign; the DLC
    /// campaigns' copies hold the same ladder), or null when it can't be read.
    /// </summary>
    public IReadOnlyList<int>? HonorThresholds { get; private init; }

    public static ClansArt? Open(string clansFolder, Oodle oodle)
    {
        var icons = new Dictionary<string, (IoStoreContainer, int)>(StringComparer.Ordinal);
        var containers = new List<IoStoreContainer>();
        foreach (var toc in Directory.EnumerateFiles(Path.Combine(clansFolder, GameInstall.ClansPaksRelative), "*.utoc"))
        {
            IoStoreContainer container;
            try
            {
                container = IoStoreContainer.Open(toc);
                containers.Add(container);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or MechWarrior5.Shared.Unreal.SaveFormatException or ArgumentException)
            {
                continue;
            }

            foreach (var (path, chunk) in container.Files)
            {
                const string Suffix = "_Icon_UIX.uasset";
                if (path.Contains("/UI/Textures/Battlemechs/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
                {
                    icons[Key(Path.GetFileName(path)[..^Suffix.Length])] = (container, chunk);
                }
            }
        }

        return icons.Count == 0 ? null : new ClansArt(oodle, icons) { HonorThresholds = ReadHonor(containers, oodle) };
    }

    private static IReadOnlyList<int>? ReadHonor(IEnumerable<IoStoreContainer> containers, Oodle oodle)
    {
        foreach (var container in containers)
        {
            try
            {
                if (container.Files.FirstOrDefault(f => f.Key.EndsWith("/Progression/Honor/HonorLevelingAsset.uasset", StringComparison.OrdinalIgnoreCase)) is { Key: not null } entry)
                {
                    var package = container.Read(entry.Value, (method, data, size) => oodle.Decompress(data, size));
                    return MechWarrior5.Clans.SaveEditor.Core.Game.HonorLadder.Parse(package);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or MechWarrior5.Shared.Unreal.SaveFormatException or ArgumentException)
            {
            }
        }

        return null;
    }

    /// <summary>The chassis' picture ("Shadow Hawk IIC" is <c>Shadowhawkiic_Icon_UIX</c>), or null.</summary>
    public BitmapSource? Load(string chassis)
    {
        if (!_icons.TryGetValue(Key(chassis), out var icon))
        {
            return null;
        }

        try
        {
            var package = icon.Container.Read(icon.Chunk, (method, data, size) =>
                string.Equals(method, "Oodle", StringComparison.OrdinalIgnoreCase) ? _oodle.Decompress(data, size)
                    : throw new InvalidDataException($"unsupported compression {method}"));
            if (TextureDecoder.DecodeZen(package) is not { } picture)
            {
                return null;
            }

            var bitmap = BitmapSource.Create(picture.Width, picture.Height, 96, 96, PixelFormats.Bgra32, null, picture.Bgra, picture.Width * 4);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return null;
        }
    }

    private static string Key(string name) => new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}

public enum ClansArtStatus
{
    NotStarted,
    Searching,
    Ready,
    ClansNotFound,
    OodleNotFound,
}
