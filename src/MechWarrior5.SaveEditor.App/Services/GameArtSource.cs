using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Shared.Assets;
using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.SaveEditor.App.Services;

/// <summary>
/// 'Mech pictures read from the installed games' own files; decoded on demand and kept in memory, nothing downloaded or
/// written. Mercenaries: the texture each variant's data asset names, from the base game, DLC and enabled mods. Clans:
/// its own pictures when an Oodle DLL is on this PC (<see cref="ClansArt"/>), else the Mercenaries picture of the same chassis.
/// </summary>
public sealed class GameArtSource(MechCatalog catalog) : IMechArtSource, IDisposable
{
    private readonly ConcurrentDictionary<GameTexture, Lazy<Task<BitmapSource?>>> _pictures = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource?>>> _clansPictures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PakFile> _paks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();
    private GameData? _game;
    private Task<ClansArt?> _clans = Task.FromResult<ClansArt?>(null);

    /// <summary>Raised (on a worker thread) when the Clans picture search finishes.</summary>
    public event EventHandler? ClansChanged;

    /// <summary>What the Clans picture search found, for Settings.</summary>
    public ClansArtStatus ClansStatus { get; private set; } = ClansArtStatus.NotStarted;

    /// <summary>The Oodle DLL in use for Clans pictures, when one was found.</summary>
    public string? OodlePath { get; private set; }

    /// <summary>Looks for the Clans install and an Oodle DLL in the background; Clans pictures wait for it.</summary>
    public Task<ClansArt?> StartClans(string? preferredOodle)
    {
        ClansStatus = ClansArtStatus.Searching;
        _clansPictures.Clear();
        _clans = Task.Run(() =>
        {
            ClansArt? art = null;
            if (GameInstall.FindClans() is not { } folder)
            {
                ClansStatus = ClansArtStatus.ClansNotFound;
            }
            else if (OodleLocator.Find(preferredOodle) is not { } oodle)
            {
                ClansStatus = ClansArtStatus.OodleNotFound;
            }
            else
            {
                OodlePath = oodle.Path;
                art = ClansArt.Open(folder, oodle);
                ClansStatus = art is null ? ClansArtStatus.ClansNotFound : ClansArtStatus.Ready;
            }

            ClansChanged?.Invoke(this, EventArgs.Empty);
            return art;
        });
        return _clans;
    }

    public Task<BitmapSource?> GetAsync(MechArtKey key) =>
        key.Asset is null ? ClansThenMercsAsync(key) : MercsAsync(key);

    private async Task<BitmapSource?> ClansThenMercsAsync(MechArtKey key)
    {
        if (await _clans is { } clans
            && await _clansPictures.GetOrAdd(key.Chassis, chassis => new Lazy<Task<BitmapSource?>>(() => Task.Run(() => clans.Load(chassis)))).Value is { } own)
        {
            return own;
        }

        return await MercsAsync(key);
    }

    private Task<BitmapSource?> MercsAsync(MechArtKey key)
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
