using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MechWarrior5.Shared.Wpf.Services;

namespace MechWarrior5.Shared.Wpf.Behaviors;

/// <summary>Binds an Image to a chassis picture by Sarna page title, loaded asynchronously.</summary>
public static class MechArt
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.RegisterAttached(
        "Title", typeof(string), typeof(MechArt), new PropertyMetadata(null, OnTitleChanged));

    /// <summary>
    /// Opens a chassis picture large. The parameter is the Sarna page title; a
    /// <see cref="Controls.MechArtLightbox"/> in the window handles it.
    /// </summary>
    public static readonly RoutedUICommand ShowCommand = new("Show mech picture", "ShowMechArt", typeof(MechArt));

    public static MechImageService? Service { get; set; }

    public static string? GetTitle(DependencyObject element) => (string?)element.GetValue(TitleProperty);

    public static void SetTitle(DependencyObject element, string? value) => element.SetValue(TitleProperty, value);

    private static async void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        image.Source = null;
        if (e.NewValue is not string title || Service is null)
        {
            return;
        }

        var source = await Service.GetAsync(title);
        // Frozen bitmaps cross threads; the Image doesn't, so finish on its own thread whatever context awaited.
        if (image.Dispatcher.CheckAccess())
        {
            Apply(image, title, source);
        }
        else
        {
            _ = image.Dispatcher.BeginInvoke(() => Apply(image, title, source));
        }
    }

    private static void Apply(Image image, string title, System.Windows.Media.ImageSource? source)
    {
        // Recycled rows may have moved on to another mech while the download ran.
        if (string.Equals(GetTitle(image), title, StringComparison.Ordinal))
        {
            image.Source = source;
        }
    }
}
