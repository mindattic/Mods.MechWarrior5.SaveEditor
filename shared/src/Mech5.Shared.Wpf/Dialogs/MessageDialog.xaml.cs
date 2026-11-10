using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Mech5.Shared.Wpf.Services;

namespace Mech5.Shared.Wpf.Dialogs;

public sealed record MessageDialogModel(
    string Title,
    string Message,
    DialogTone Tone,
    IReadOnlyList<DialogSection> RawSections,
    string PrimaryText,
    string? SecondaryText,
    string? TertiaryText)
{
    public Brush ToneBrush => BrushFor(Tone);

    public IEnumerable<object> Sections => RawSections.Select(s => new { s.Header, s.Lines, Brush = BrushFor(s.Tone) });

    public Visibility SecondaryVisibility => SecondaryText is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TertiaryVisibility => TertiaryText is null ? Visibility.Collapsed : Visibility.Visible;

    private static Brush BrushFor(DialogTone tone) => (Brush)Application.Current.FindResource(tone switch
    {
        DialogTone.Success => "Brush.Ok",
        DialogTone.Warning => "Brush.Amber",
        DialogTone.Error => "Brush.Danger",
        _ => "Brush.Info",
    });
}

public partial class MessageDialog : Window
{
    private bool? _result;

    private MessageDialog(MessageDialogModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    /// <summary>Primary = true, secondary (cancel) = null, tertiary = false.</summary>
    public static bool? Run(Window? owner, MessageDialogModel model)
    {
        var dialog = new MessageDialog(model)
        {
            Owner = owner,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
        };
        dialog.ShowDialog();
        return model.SecondaryText is null && dialog._result is null ? true : dialog._result;
    }

    private void OnPrimary(object sender, RoutedEventArgs e) => Close(true);

    private void OnSecondary(object sender, RoutedEventArgs e) => Close(null);

    private void OnTertiary(object sender, RoutedEventArgs e) => Close(false);

    private void Close(bool? result)
    {
        _result = result;
        Close();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
