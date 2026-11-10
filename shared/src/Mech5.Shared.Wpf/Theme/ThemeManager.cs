using System.Windows;

namespace Mech5.Shared.Wpf.Theme;

public enum Mech5Theme
{
    /// <summary>MW5 Mercenaries: gunmetal and amber.</summary>
    Mercs,

    /// <summary>MW5 Clans: cold steel and ice cyan.</summary>
    Clans,
}

/// <summary>
/// Swaps the palette dictionary inside the app's merged <c>Mech5Theme.xaml</c>. Every style and view
/// references brushes with DynamicResource, so a swap restyles open windows immediately.
/// </summary>
public static class ThemeManager
{
    private const string PalettePrefix = "pack://application:,,,/Mech5.Shared.Wpf;component/Theme/Palette.";

    public static event EventHandler? ThemeChanged;

    public static Mech5Theme Current { get; private set; } = Mech5Theme.Mercs;

    public static bool TryParse(string? name, out Mech5Theme theme) => Enum.TryParse(name, ignoreCase: true, out theme);

    public static void Apply(Mech5Theme theme)
    {
        if (Application.Current?.Resources is not { } resources || Find(resources) is not { } owner)
        {
            throw new InvalidOperationException("Mech5Theme.xaml is not merged into the application resources.");
        }

        var index = owner.MergedDictionaries.ToList().FindIndex(IsPalette);
        owner.MergedDictionaries[index] = new ResourceDictionary { Source = new Uri($"{PalettePrefix}{theme}.xaml", UriKind.Absolute) };
        Current = theme;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static ResourceDictionary? Find(ResourceDictionary dictionary)
    {
        if (dictionary.MergedDictionaries.Any(IsPalette))
        {
            return dictionary;
        }

        return dictionary.MergedDictionaries.Select(Find).FirstOrDefault(found => found is not null);
    }

    private static bool IsPalette(ResourceDictionary dictionary) =>
        dictionary.Source?.OriginalString.StartsWith(PalettePrefix, StringComparison.OrdinalIgnoreCase) == true;
}
