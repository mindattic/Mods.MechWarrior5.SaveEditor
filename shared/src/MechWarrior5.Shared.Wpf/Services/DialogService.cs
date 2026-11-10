using System.IO;
using System.Windows;
using Microsoft.Win32;
using MechWarrior5.Shared.Wpf.Dialogs;

namespace MechWarrior5.Shared.Wpf.Services;

public sealed class DialogService : IDialogService
{
    /// <summary>Translates this service's own wording: (key, English) to text. The app sets it; English by default.</summary>
    public static Func<string, string, string> Text { get; set; } = (_, english) => english;

    private static string SaveFilter => Text("dlg_save_filter", "MW5 campaign save (*.sav)|*.sav|All files (*.*)|*.*");

    public string? PickSaveToOpen(string? initialFolder)
    {
        var dialog = new OpenFileDialog
        {
            Title = Text("dlg_open_save", "Open MW5 campaign save"),
            Filter = SaveFilter,
            InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : string.Empty,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickSaveAs(string currentPath)
    {
        var dialog = new SaveFileDialog
        {
            Title = Text("dlg_save_copy_as", "Save campaign copy as"),
            Filter = SaveFilter,
            InitialDirectory = Path.GetDirectoryName(currentPath) ?? string.Empty,
            FileName = Path.GetFileName(currentPath),
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFileToOpen(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFileToSave(string title, string defaultName, string filter)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultName, OverwritePrompt = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void Show(string title, string message, DialogTone tone = DialogTone.Info, params DialogSection[] sections) =>
        MessageDialog.Run(Owner, new MessageDialogModel(title, message, tone, sections, Text("dlg_ok", "OK"), null, null));

    public bool Confirm(string title, string message, string confirmText, DialogTone tone = DialogTone.Warning) =>
        MessageDialog.Run(Owner, new MessageDialogModel(title, message, tone, [], confirmText, Text("dlg_cancel", "Cancel"), null)) == true;

    public bool? AskSaveChanges(string fileName) =>
        MessageDialog.Run(Owner, new MessageDialogModel(
            Text("dlg_unsaved_changes", "Unsaved changes"),
            string.Format(System.Globalization.CultureInfo.CurrentCulture, Text("dlg_save_changes_first", "Save your changes to {0} first?"), fileName),
            DialogTone.Warning,
            [],
            Text("dlg_save", "Save"),
            Text("dlg_cancel", "Cancel"),
            Text("dlg_discard", "Discard")));

    private static Window? Owner => Application.Current?.MainWindow is { IsLoaded: true } w ? w : null;
}
