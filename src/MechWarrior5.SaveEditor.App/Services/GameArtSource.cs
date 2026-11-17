using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Shared.Assets;
using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.SaveEditor.App.Services;

/// <summary>
/// 'Mech pictures read from the installed MechWarrior 5: Mercenaries paks (base game, DLC and enabled mods), the
/// textures each variant's data asset names. Decoded on demand and kept in memory; nothing is downloaded or written.
/// Clans 'Mechs use the Mercenaries picture of the same chassis, since Clans' own files need a decompressor
/// players don't have.
/// </summary>
public sealed class GameArtSource(MechCatalog catalog) : IMechArtSource, IDisposable
{
    private readonly ConcurrentDictionary<GameTexture, Lazy<Task<BitmapSource?>>> _pictures = new();
    private readonly Dictionary<string, PakFile> _paks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();
    private GameData? _game;

    public Task<BitmapSource?> GetAsync(MechArtKey key)
    {
        if (catalog.Game is not { } game)
        {
            return Task.FromResult<BitmapSource?>(null);
        }

        if (!ReferenceEquals(game, _game))
        {
            // A rescan may have changed the game or its mods: forget pictures and pak handles from the last one.
            lock (_gate)
            {
                Close();
                _pictures.Clear();
                _game = game;
            }
        }

        return Find(game, key) is { } texture
            ? _pictures.GetOrAdd(texture, t => new Lazy<Task<BitmapSource?>>(() => Task.Run(() => Load(t)))).Value
            : Task.FromResult<BitmapSource?>(null);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Close();
        }
    }

    private GameTexture? Find(GameData game, MechArtKey key)
    {
        if (key.Asset is { } asset && game.MechArt.TryGetValue(asset, out var own))
        {
            return own;
        }

        return catalog.Variants
            .Where(v => string.Equals(v.ChassisName, key.Chassis, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.HeroName is not null)
            .Select(v => game.MechArt.GetValueOrDefault(v.Asset))
            .FirstOrDefault(t => t is not null);
    }

    private BitmapSource? Load(GameTexture texture)
    {
        byte[] uasset, uexp;
        byte[]? ubulk;
        try
        {
            lock (_gate)
            {
                if (!_paks.TryGetValue(texture.PakPath, out var pak))
                {
                    _paks[texture.PakPath] = pak = PakFile.Open(texture.PakPath);
                }

                var stem = texture.Entry[..^".uasset".Length];
                uasset = pak.Read(texture.Entry);
                uexp = pak.Read(stem + ".uexp");
                ubulk = pak.Contains(stem + ".ubulk") ? pak.Read(stem + ".ubulk") : null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or InvalidDataException)
        {
            return null;
        }

        if (TextureDecoder.Decode(uasset, uexp, ubulk) is not { } picture)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(picture.Width, picture.Height, 96, 96, PixelFormats.Bgra32, null, picture.Bgra, picture.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private void Close()
    {
        foreach (var pak in _paks.Values)
        {
            pak.Dispose();
        }

        _paks.Clear();
    }
}
