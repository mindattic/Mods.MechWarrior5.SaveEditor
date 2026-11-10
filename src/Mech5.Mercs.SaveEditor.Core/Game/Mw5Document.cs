using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Saves;

namespace Mech5.Mercs.SaveEditor.Core.Game;

/// <summary>Where a save went: a new file (registered in the campaign index or not) or in place.</summary>
public sealed record SaveOutcome(string Path, bool CreatedNewFile, bool RegisteredInCampaign);

/// <summary>An open campaign save and the editors over its models.</summary>
public sealed class Mw5Document
{
    private Mw5Document(string path, Mw5SaveFile file, MechCatalog catalog)
    {
        FilePath = path;
        File = file;
        Dlc = DlcOwnership.ForSave(path);
        Company = new CompanyEditor(file, MarkDirty);
        Factions = new FactionEditor(file, MarkDirty);
        Inventory = new InventoryEditor(file, MarkDirty);
        Roster = new RosterEditor(file, MarkDirty);
        Hangar = new HangarEditor(file, catalog, Dlc, MarkDirty);
    }

    public event EventHandler? DirtyChanged;

    public string FilePath { get; private set; }

    public Mw5SaveFile File { get; }

    public DlcOwnership Dlc { get; }

    public CompanyEditor Company { get; }

    public FactionEditor Factions { get; }

    public InventoryEditor Inventory { get; }

    public RosterEditor Roster { get; }

    public HangarEditor Hangar { get; }

    public bool IsDirty { get; private set; }

    public static Mw5Document Open(string path, MechCatalog catalog) =>
        new(path, Mw5SaveFile.Load(path), catalog);

    /// <summary>True when this file was written by the game, so saving must create a new file.</summary>
    public bool IsOriginalGameSave => File.Stamp is null || !EditorStamp.FileHasStamp(FilePath);

    /// <summary>The save's name in the game's load menu (from Campaign.json), if listed.</summary>
    public string? DisplayName => CampaignIndex.GetName(
        System.IO.Path.GetDirectoryName(FilePath) ?? ".", System.IO.Path.GetFileNameWithoutExtension(FilePath));

    /// <summary>
    /// Saves the edits. An original game save is never overwritten: the edits go to a new GUID-named
    /// save beside it, registered in <c>Campaign.json</c> under <paramref name="displayName"/> so the
    /// game's load menu lists it. A save this editor wrote is updated in place (previous version kept as
    /// <c>.bak</c>) and renamed in the load menu when <paramref name="displayName"/> changed. An explicit
    /// <paramref name="path"/> may be a new file or another editor-written save, never a game save.
    /// </summary>
    public SaveOutcome Save(string? path = null, string? displayName = null)
    {
        Hangar.Commit();
        var source = System.IO.Path.GetFileNameWithoutExtension(FilePath);
        var createNew = path is null && IsOriginalGameSave;
        var target = path
            ?? (createNew
                ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FilePath) ?? ".", Guid.NewGuid().ToString("N").ToUpperInvariant() + ".sav")
                : FilePath);

        var isNewFile = !System.IO.File.Exists(target);
        File.Save(target, source, backup: true);

        var folder = System.IO.Path.GetDirectoryName(target) ?? ".";
        var file = System.IO.Path.GetFileNameWithoutExtension(target);
        var registered = isNewFile
            ? CampaignIndex.RegisterCopy(folder, source, file, displayName, Company.CampaignDate)
            : CampaignIndex.Update(folder, file, displayName, Company.CampaignDate);

        FilePath = target;
        SetDirty(false);
        return new SaveOutcome(target, isNewFile, registered);
    }

    private void MarkDirty() => SetDirty(true);

    private void SetDirty(bool value)
    {
        if (IsDirty == value)
        {
            return;
        }

        IsDirty = value;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }
}
