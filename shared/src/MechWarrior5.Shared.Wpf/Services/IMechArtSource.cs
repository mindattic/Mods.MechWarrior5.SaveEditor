using System.Windows.Media.Imaging;

namespace MechWarrior5.Shared.Wpf.Services;

/// <summary>
/// Which 'Mech picture to show: the variant's own data asset when known (e.g. <c>AS7-D_MDA</c>), else any picture
/// of the chassis by name. <see cref="Chassis"/> is also the picture's caption.
/// </summary>
public sealed record MechArtKey(string? Asset, string Chassis);

/// <summary>
/// Supplies 'Mech pictures from local files only (the player's installed game); never the network.
/// Returns frozen bitmaps, or null when there is no picture.
/// </summary>
public interface IMechArtSource
{
    Task<BitmapSource?> GetAsync(MechArtKey key);
}
