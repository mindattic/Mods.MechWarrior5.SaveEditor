using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MechWarrior5.Shared.Wpf.Behaviors;
using MechWarrior5.Shared.Wpf.Services;

namespace MechWarrior5.Shared.Wpf.Controls;

/// <summary>
/// Shows a chassis picture large when <see cref="MechArt.ShowCommand"/> runs anywhere in its window.
/// Put one in each window's root grid, spanning every row.
/// </summary>
public partial class MechArtLightbox : UserControl
{
    public static readonly DependencyProperty LinkTextProperty = DependencyProperty.Register(
        nameof(LinkText), typeof(string), typeof(MechArtLightbox), new PropertyMetadata("VIEW ON SARNA.NET"));

    public static readonly DependencyProperty LoadingTextProperty = DependencyProperty.Register(
        nameof(LoadingText), typeof(string), typeof(MechArtLightbox), new PropertyMetadata("Loading picture…"));

    public static readonly DependencyProperty NoPictureTextProperty = DependencyProperty.Register(
        nameof(NoPictureText), typeof(string), typeof(MechArtLightbox), new PropertyMetadata("No picture available"));

    private readonly CommandBinding _binding;
    private Window? _host;
    private string? _title;

    public MechArtLightbox()
    {
        InitializeComponent();
        // The footer lives in MaModal's Footer property, outside this control's name scope, so bind it here.
        LinkLabel.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(LinkText)) { Source = this });
        // The app's implicit TextBlock style would otherwise win over the link's state colour.
        LinkLabel.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(SarnaLink.EffectiveForeground)) { Source = SarnaLink });
        _binding = new CommandBinding(MechArt.ShowCommand, OnShow, OnCanShow);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>The footer link's text (localise it from the host).</summary>
    public string LinkText
    {
        get => (string)GetValue(LinkTextProperty);
        set => SetValue(LinkTextProperty, value);
    }

    /// <summary>Shown while the large picture downloads.</summary>
    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    /// <summary>Shown when Sarna has no picture or can't be reached.</summary>
    public string NoPictureText
    {
        get => (string)GetValue(NoPictureTextProperty);
        set => SetValue(NoPictureTextProperty, value);
    }

    /// <summary>True while a picture is showing.</summary>
    public bool IsOpen => Modal.IsOpen;

    /// <summary>The Sarna title being shown, or null.</summary>
    public string? ShownTitle => Modal.IsOpen ? _title : null;

    /// <summary>Opens the lightbox on a chassis (also what <see cref="MechArt.ShowCommand"/> does).</summary>
    public async void Show(string sarnaTitle)
    {
        _title = sarnaTitle;
        Modal.Title = sarnaTitle;
        Picture.Source = null;
        Status.Text = LoadingText;
        // As large as the window allows, leaving room for the card's title, footer and margins.
        var room = (_host?.ActualHeight ?? ActualHeight) - 240;
        PictureArea.Height = Math.Clamp(double.IsNaN(room) ? 640 : room, 240, 760);
        Modal.IsOpen = true;

        var source = MechArt.Service is null ? null : await MechArt.Service.GetAsync(sarnaTitle, MechImageService.LargeWidth);
        // Frozen bitmaps cross threads; the control doesn't, so finish on the UI thread whatever context awaited.
        if (Dispatcher.CheckAccess())
        {
            ShowPicture(sarnaTitle, source);
        }
        else
        {
            _ = Dispatcher.BeginInvoke(() => ShowPicture(sarnaTitle, source));
        }
    }

    private void ShowPicture(string sarnaTitle, BitmapSource? source)
    {
        // The user may have opened another mech while the download ran.
        if (!string.Equals(_title, sarnaTitle, StringComparison.Ordinal))
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
        e.CanExecute = e.Parameter is string { Length: > 0 };

    private void OnShow(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string { Length: > 0 } title)
        {
            Show(title);
            e.Handled = true;
        }
    }

    private void OnOpenSarna(object sender, RoutedEventArgs e)
    {
        if (_title is { Length: > 0 } title)
        {
            Process.Start(new ProcessStartInfo(MechImageService.PageUrl(title).AbsoluteUri) { UseShellExecute = true });
        }
    }
}
