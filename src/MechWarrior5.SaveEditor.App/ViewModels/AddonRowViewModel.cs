using CommunityToolkit.Mvvm.ComponentModel;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;
using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

/// <summary>An add-on pack in Settings: what it is, the mod it needs, whether it applies, and its switch.</summary>
public sealed partial class AddonRowViewModel(AddonEntry entry, Action<string, bool> setOn) : ObservableObject
{
    public string Name => entry.Name;

    public string File => entry.File;

    /// <summary>The mod the pack needs, with the versions it supports, and what is installed.</summary>
    public string Requires => entry.Pack is { } pack
        ? Loc.Text("addon_requires", "Needs {0} {1}", pack.Requires.Label,
              pack.Requires.Versions.Count == 0 ? Loc.Text("addon_any_version", "(any version)") : string.Join(" / ", pack.Requires.Versions))
          + (entry.Mod is { } mod ? Loc.Text("addon_installed", "  -  installed: {0}", mod.Label) : string.Empty)
        : string.Empty;

    public string? Source => entry.Pack?.Source;

    public bool IsActive => entry.Status == AddonStatus.Active;

    public bool IsProblem => entry.Status is AddonStatus.Invalid or AddonStatus.VersionNotSupported or AddonStatus.NeedsPack;

    public bool CanToggle => entry.Pack is not null && entry.Status is not (AddonStatus.Invalid or AddonStatus.NotForThisGame or AddonStatus.AllTurnedOff);

    public bool IsOn
    {
        get => entry.Status != AddonStatus.TurnedOff;
        set => setOn(entry.Name, value);
    }

    public string Status => entry.Status switch
    {
        AddonStatus.Active => Loc.Text("addon_active", "ACTIVE"),
        AddonStatus.TurnedOff => Loc.Text("addon_turned_off", "Turned off"),
        AddonStatus.AllTurnedOff => Loc.Text("addon_all_off", "Off (all add-ons off)"),
        AddonStatus.ModNotInstalled => Loc.Text("addon_not_installed", "Mod not installed"),
        AddonStatus.ModDisabled => Loc.Text("addon_mod_disabled", "Mod disabled in the game"),
        AddonStatus.VersionNotSupported => Loc.Text("addon_wrong_version", "Mod version {0} not supported", entry.Detail ?? "?"),
        AddonStatus.NeedsPack => Loc.Text("addon_needs_pack", "Needs the \"{0}\" add-on", entry.Detail ?? "?"),
        AddonStatus.NotForThisGame => Loc.Text("addon_other_game", "Not for this game ({0})", entry.Detail ?? "?"),
        _ => Loc.Text("addon_invalid", "Can't be read: {0}", entry.Detail ?? "?"),
    };
}
