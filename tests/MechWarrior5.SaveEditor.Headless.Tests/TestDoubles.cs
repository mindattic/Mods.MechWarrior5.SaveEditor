using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Shared.Wpf.Services;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>Answers every dialog without a person: confirms, never picks files, records what was shown.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public List<string> Shown { get; } = [];

    public List<string> Errors { get; } = [];

    public string? PickSaveToOpen(string? initialFolder) => null;

    public string? PickSaveAs(string currentPath) => null;

    public string? PickFileToOpen(string title, string filter) => null;

    public string? PickFileToSave(string title, string defaultName, string filter) => null;

    public void Show(string title, string message, DialogTone tone = DialogTone.Info, params DialogSection[] sections)
    {
        Shown.Add($"{title}: {message}");
        if (tone == DialogTone.Error)
        {
            Errors.Add($"{title}: {message}");
        }
    }

    public bool Confirm(string title, string message, string confirmText, DialogTone tone = DialogTone.Warning) => true;

    public bool? AskSaveChanges(string fileName) => false;
}

/// <summary>Stands in for the Mech Lab window: makes the edit the test asked for, then applies it.</summary>
internal sealed class FakeEditorWindows(Action<LoadoutEditorViewModel> edit) : IEditorWindows
{
    public int Opened { get; private set; }

    public bool EditLoadout(LoadoutEditorViewModel editor)
    {
        Opened++;
        var applied = false;
        editor.CloseRequested += (_, ok) => applied = ok;
        edit(editor);
        editor.ApplyCommand.Execute(null);
        return applied;
    }
}
