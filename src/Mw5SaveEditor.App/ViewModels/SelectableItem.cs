using CommunityToolkit.Mvvm.ComponentModel;

namespace Mw5SaveEditor.App.ViewModels;

/// <summary>A list row that can be ticked for batch actions.</summary>
public abstract partial class SelectableItem : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    public event EventHandler? CheckedChanged;

    partial void OnIsCheckedChanged(bool value) => CheckedChanged?.Invoke(this, EventArgs.Empty);
}
