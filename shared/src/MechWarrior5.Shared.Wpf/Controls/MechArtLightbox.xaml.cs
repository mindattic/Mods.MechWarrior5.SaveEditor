using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MechWarrior5.Shared.Wpf.Behaviors;
using MechWarrior5.Shared.Wpf.Services;

namespace MechWarrior5.Shared.Wpf.Controls;

/// <summary>
/// Shows a 'Mech picture large when <see cref="MechArt.ShowCommand"/> runs anywhere in its window.
/// Put one in each window's root grid, spanning every row.
/// </summary>
public partial class MechArtLightbox : UserControl
{
    public static readonly DependencyProperty LoadingTextProperty = DependencyProperty.Register(
        nameof(LoadingText), typeof(string), typeof(MechArtLightbox), new PropertyMetadata("Loading picture…"));

    public static readonly DependencyProperty NoPictureTextProperty = DependencyProperty.Register(
        nameof(NoPictureText), typeof(string), typeof(MechArtLightbox), new PropertyMetadata("No picture available"));

    private readonly CommandBinding _binding;
    private Window? _host;
    private MechArtKey? _key;

    public MechArtLightbox()
    {
        InitializeComponent();
        _binding = new CommandBinding(MechArt.ShowCommand, OnShow, OnCanShow);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>Shown while the large picture loads.</summary>
    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    /// <summary>Shown when the installed game has no picture for this 'Mech.</summary>
    public string NoPictureText
    {
        get => (string)GetValue(NoPictureTextProperty);
        set => SetValue(NoPictureTextProperty, value);
    }

    /// <summary>True while a picture is showing.</summary>
    public bool IsOpen => Modal.IsOpen;

    /// <summary>The caption of the 'Mech being shown, or null.</summary>
    public string? ShownTitle => Modal.IsOpen ? _key?.Chassis : null;

    /// <summary>Opens the lightbox on a 'Mech (also what <see cref="MechArt.ShowCommand"/> does).</summary>
    public async void Show(MechArtKey key)
    {
        _key = key;
        Modal.Title = key.Chassis;
        Picture.Source = null;
        Status.Text = LoadingText;
        // As large as the window allows, leaving room for the card's title and margins.
        var room = (_host?.ActualHeight ?? ActualHeight) - 200;
        PictureArea.Height = Math.Clamp(double.IsNaN(room) ? 640 : room, 240, 760);
        Modal.IsOpen = true;

        var source = MechArt.Service is { } service ? await service.GetAsync(key) : null;
        // Frozen bitmaps cross threads; the control doesn't, so finish on the UI thread whatever context awaited.
        if (Dispatcher.CheckAccess())
        {
            ShowPicture(key, source);
        }
        else
        {
            _ = Dispatcher.BeginInvoke(() => ShowPicture(key, source));
        }
    }

    private void ShowPicture(MechArtKey key, BitmapSource? source)
    {
        // The user may have opened another 'Mech while this one loaded.
        if (!Equals(_key, key))
        {
            return;
        }

        Picture.Source = source;
        Status.Text = source is null ? NoPictureText : string.Empty;
    }

    /// <summary>Closes the lightbox.</summary>
    public void Close() => Modal.IsOpen = false;

    private void Attach()
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _host))
        {
            return;
        }

        Detach();
        _host = window;
        _host?.CommandBindings.Add(_binding);
    }

    private void Detach()
    {
        _host?.CommandBindings.Remove(_binding);
        _host = null;
    }

    private static void OnCanShow(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = e.Parameter is MechArtKey;

    private void OnShow(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is MechArtKey key)
        {
            Show(key);
            e.Handled = true;
        }
    }
}
