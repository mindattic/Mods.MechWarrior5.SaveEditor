using System.Windows;
using System.Windows.Media;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;
using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

/// <summary>One line of the compatibility report, worded for display.</summary>
public sealed class CompatibilityRowViewModel(CompatibilityLine line)
{
    public CompatibilityLine Line => line;

    public string SeverityLabel => line.Severity == CompatibilitySeverity.Problem
        ? Loc.Text("compat_problem", "NEEDS MOD")
        : Loc.Text("compat_modded", "MODDED");

    public Brush? SeverityBrush => Application.Current?.TryFindResource(line.Severity == CompatibilitySeverity.Problem ? "Brush.Danger" : "Brush.AccentHot") as Brush;

    /// <summary>The mod and version behind the line, or "Unknown mod".</summary>
    public string ModLabel => line.Mod is null
        ? Loc.Text("compat_unknown_mod", "Unknown mod:")
        : $"{line.Mod}{(line.Version is { } v ? " " + v : string.Empty)}:";
}
