namespace Mech5.Shared.Wpf.Services;

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

    /// <summary>Open-file picker; <paramref name="filter"/> uses the <c>Label|*.ext</c> form.</summary>
    string? PickFileToOpen(string title, string filter);

    string? PickFileToSave(string title, string defaultName, string filter);

    void Show(string title, string message, DialogTone tone = DialogTone.Info, params DialogSection[] sections);

    bool Confirm(string title, string message, string confirmText, DialogTone tone = DialogTone.Warning);

    /// <summary>Yes = true, No = false, Cancel = null.</summary>
    bool? AskSaveChanges(string fileName);
}
