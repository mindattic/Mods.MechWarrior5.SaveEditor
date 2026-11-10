using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MechWarrior5.Shared.Wpf.Services;

namespace MechWarrior5.Shared.Wpf.Dialogs;

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

    // Buttons show their text upper-cased like the app's own buttons; callers' text keys stay as they are.
    public string PrimaryLabel => Upper(PrimaryText);

    public string? SecondaryLabel => SecondaryText is null ? null : Upper(SecondaryText);

    public string? TertiaryLabel => TertiaryText is null ? null : Upper(TertiaryText);

    public Visibility SecondaryVisibility => SecondaryText is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TertiaryVisibility => TertiaryText is null ? Visibility.Collapsed : Visibility.Visible;

    private static string Upper(string text) => text.ToUpper(CultureInfo.CurrentUICulture);

    private static Brush BrushFor(DialogTone tone) => (Brush)Application.Current.FindResource(tone switch
    {
        DialogTone.Success => "Brush.Ok",
        DialogTone.Warning => "Brush.Accent",
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

        // A destructive confirm (Error tone) gets the same red danger preset as the app's REMOVE and the dialog's DISCARD buttons.
        if (model.Tone == DialogTone.Error && model.SecondaryText is not null)
        {
            PrimaryButton.Style = (Style)FindResource("Button.Danger");
        }
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
