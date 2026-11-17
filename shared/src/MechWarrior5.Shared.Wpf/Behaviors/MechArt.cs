using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MechWarrior5.Shared.Wpf.Services;

namespace MechWarrior5.Shared.Wpf.Behaviors;

/// <summary>Binds an Image to a 'Mech picture, loaded asynchronously from the installed game.</summary>
public static class MechArt
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(MechArtKey), typeof(MechArt), new PropertyMetadata(null, OnKeyChanged));

    /// <summary>
    /// Opens a 'Mech picture large. The parameter is its <see cref="MechArtKey"/>; a
    /// <see cref="Controls.MechArtLightbox"/> in the window handles it.
    /// </summary>
    public static readonly RoutedUICommand ShowCommand = new("Show mech picture", "ShowMechArt", typeof(MechArt));

    public static IMechArtSource? Service { get; set; }

    public static MechArtKey? GetKey(DependencyObject element) => (MechArtKey?)element.GetValue(KeyProperty);

    public static void SetKey(DependencyObject element, MechArtKey? value) => element.SetValue(KeyProperty, value);

    private static async void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        image.Source = null;
        if (e.NewValue is not MechArtKey key || Service is null)
        {
            return;
        }

        var source = await Service.GetAsync(key);
        // Frozen bitmaps cross threads; the Image doesn't, so finish on its own thread whatever context awaited.
        if (image.Dispatcher.CheckAccess())
        {
            Apply(image, key, source);
        }
        else
        {
            _ = image.Dispatcher.BeginInvoke(() => Apply(image, key, source));
        }
    }

    private static void Apply(Image image, MechArtKey key, System.Windows.Media.ImageSource? source)
    {
        // Recycled rows may have moved on to another 'Mech while the picture loaded.
        if (Equals(GetKey(image), key))
        {
            image.Source = source;
        }
    }
}
