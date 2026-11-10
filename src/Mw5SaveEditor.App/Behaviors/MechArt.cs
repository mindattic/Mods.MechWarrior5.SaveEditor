using System.Windows;
using System.Windows.Controls;
using Mw5SaveEditor.App.Services;

namespace Mw5SaveEditor.App.Behaviors;

/// <summary>Binds an Image to a chassis picture by Sarna page title, loaded asynchronously.</summary>
public static class MechArt
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.RegisterAttached(
        "Title", typeof(string), typeof(MechArt), new PropertyMetadata(null, OnTitleChanged));

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
        // Recycled rows may have moved on to another mech while the download ran.
        if (string.Equals(GetTitle(image), title, StringComparison.Ordinal))
        {
            image.Source = source;
        }
    }
}
