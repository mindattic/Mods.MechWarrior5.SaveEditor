namespace Mw5SaveEditor.App.Services;

public enum DialogTone
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record DialogSection(string Header, DialogTone Tone, IReadOnlyList<string> Lines);

public interface IDialogService
{
    string? PickSaveToOpen(string? initialFolder);

    string? PickSaveAs(string currentPath);

    void Show(string title, string message, DialogTone tone = DialogTone.Info, params DialogSection[] sections);

    bool Confirm(string title, string message, string confirmText, DialogTone tone = DialogTone.Warning);

    /// <summary>Yes = true, No = false, Cancel = null.</summary>
    bool? AskSaveChanges(string fileName);
}
