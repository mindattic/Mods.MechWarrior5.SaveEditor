using System.Windows;

namespace MechWarrior5.Shared.Wpf.Theme;

public enum MechWarrior5Theme
{
    /// <summary>MW5 Mercenaries: gunmetal and amber.</summary>
    Mercs,

    /// <summary>MW5 Clans: cold steel and ice cyan.</summary>
    Clans,
}

/// <summary>
/// Applies a palette as a top-level merged dictionary of the application, after <c>MechWarrior5Theme.xaml</c>
/// (whose built-in Mercs palette is only the default). Only top-level changes raise the resource-changed
/// notification that makes every DynamicResource brush in open windows re-resolve, so a swap restyles
/// the whole UI at once.
/// </summary>
public static class ThemeManager
{
    private const string PalettePrefix = "pack://application:,,,/MechWarrior5.Shared.Wpf;component/Theme/Palette.";

    public static event EventHandler? ThemeChanged;

    public static MechWarrior5Theme Current { get; private set; } = MechWarrior5Theme.Mercs;

    public static bool TryParse(string? name, out MechWarrior5Theme theme) => Enum.TryParse(name, ignoreCase: true, out theme);

    public static void Apply(MechWarrior5Theme theme)
    {
        if (Application.Current?.Resources is not { } resources)
        {
            throw new InvalidOperationException("There is no running application to theme.");
        }

        var palette = new ResourceDictionary { Source = new Uri($"{PalettePrefix}{theme}.xaml", UriKind.Absolute) };
        var merged = resources.MergedDictionaries;
        var index = merged.ToList().FindIndex(d =>
            d.Source?.OriginalString.StartsWith(PalettePrefix, StringComparison.OrdinalIgnoreCase) == true);
        if (index >= 0)
        {
            merged[index] = palette;
        }
        else
        {
            merged.Add(palette);
        }

        Current = theme;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }
}
