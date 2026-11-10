using System.Windows;
using Mech5.SaveEditor.App.ViewModels;
using Mech5.SaveEditor.App.Views;

namespace Mech5.SaveEditor.App.Services;

/// <summary>Opens the editor's secondary windows; keeps view models free of window types.</summary>
public interface IEditorWindows
{
    /// <summary>Returns true when the loadout was applied.</summary>
    bool EditLoadout(LoadoutEditorViewModel editor);
}

public sealed class EditorWindows : IEditorWindows
{
    public bool EditLoadout(LoadoutEditorViewModel editor)
    {
        var window = new LoadoutEditorWindow(editor) { Owner = Application.Current?.MainWindow };
        return window.ShowDialog() == true;
    }
}
