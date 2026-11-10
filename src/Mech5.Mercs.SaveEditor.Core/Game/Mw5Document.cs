using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Saves;

namespace Mech5.Mercs.SaveEditor.Core.Game;

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

    /// <summary>Saves to <paramref name="path"/> (default: in place), keeping a <c>.bak</c> of what it replaces.</summary>
    public void Save(string? path = null)
    {
        Hangar.Commit();
        var target = path ?? FilePath;
        File.Save(target, backup: true);
        FilePath = target;
        SetDirty(false);
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
