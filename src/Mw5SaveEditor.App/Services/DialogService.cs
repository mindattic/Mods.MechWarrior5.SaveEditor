using System.IO;
using System.Windows;
using Microsoft.Win32;
using Mw5SaveEditor.App.Views;

namespace Mw5SaveEditor.App.Services;

public sealed class DialogService : IDialogService
{
    private const string SaveFilter = "MW5 campaign save (*.sav)|*.sav|All files (*.*)|*.*";

    public string? PickSaveToOpen(string? initialFolder)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open MW5 campaign save",
            Filter = SaveFilter,
            InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : string.Empty,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickSaveAs(string currentPath)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save campaign copy as",
            Filter = SaveFilter,
            InitialDirectory = Path.GetDirectoryName(currentPath) ?? string.Empty,
            FileName = Path.GetFileName(currentPath),
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void Show(string title, string message, DialogTone tone = DialogTone.Info, params DialogSection[] sections) =>
        MessageDialog.Run(Owner, new MessageDialogModel(title, message, tone, sections, "OK", null, null));

    public bool Confirm(string title, string message, string confirmText, DialogTone tone = DialogTone.Warning) =>
        MessageDialog.Run(Owner, new MessageDialogModel(title, message, tone, [], confirmText, "Cancel", null)) == true;

    public bool? AskSaveChanges(string fileName) =>
        MessageDialog.Run(Owner, new MessageDialogModel(
            "Unsaved changes",
            $"Save your changes to {fileName} first?",
            DialogTone.Warning,
            [],
            "Save",
            "Cancel",
            "Discard"));

    private static Window? Owner => Application.Current?.MainWindow is { IsLoaded: true } w ? w : null;
}
