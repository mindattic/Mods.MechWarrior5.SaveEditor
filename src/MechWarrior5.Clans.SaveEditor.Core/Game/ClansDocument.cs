using System.Reflection;
using MechWarrior5.Clans.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Saves;

namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>How a save was written.</summary>
public sealed record SaveOutcome(string Path, bool CreatedNewFile, bool RegisteredInLoadMenu);

/// <summary>
/// An open MW5 Clans campaign save. Edits go to the parsed object tree; <see cref="Save"/> writes it
/// back. An original game save is never overwritten: it is saved as a new file and listed in the
/// game's load menu under the chosen name. Only saves this editor wrote (stamped) are updated in place.
/// </summary>
public sealed class ClansDocument
{
    public const string ToolName = "MechWarrior5.Clans.SaveEditor";

    private readonly Ue5SaveFile _file;
    private readonly (ByteArrayStorage Storage, Ue5ObjectBlob Blob) _models;
    private readonly (ByteArrayStorage Storage, Ue5ObjectBlob Blob) _inventory;
    private readonly (ByteArrayStorage Storage, Ue5ObjectBlob Blob)? _deployment;

    private ClansDocument(string path, Ue5SaveFile file)
    {
        Path = path;
        _file = file;
        _models = Expand("PersistentModelData");
        _inventory = Expand("InventoryData");
        _deployment = _file.Properties.Find<ArrayProperty>("DeploymentData") is not null ? Expand("DeploymentData") : null;
        Inventory = new InventoryEditor(_inventory.Blob.Properties.Get<ArrayProperty>("Array").Structs, IsReferenced);
        Progress = new ProgressEditor(_models.Blob.Properties);
        IsOriginalGameSave = EditorStamp.Read(_file.Properties) is null;
    }

    public string Path { get; private set; }

    public InventoryEditor Inventory { get; }

    public ProgressEditor Progress { get; }

    /// <summary>True for a save the game wrote; it will be saved as a new file.</summary>
    public bool IsOriginalGameSave { get; private set; }

    /// <summary>The campaign profile this save belongs to (file name part before the underscore).</summary>
    public string ProfileKey => SaveName.Split('_')[0];

    /// <summary>The save's own id (file name part after the underscore).</summary>
    public string SaveId => SaveName.Contains('_', StringComparison.Ordinal) ? SaveName.Split('_')[1] : SaveName;

    private string SaveName => System.IO.Path.GetFileNameWithoutExtension(Path);

    public static ClansDocument Load(string path) => new(path, Ue5SaveFile.Load(path));

    /// <summary>The load-menu name of this save, from the game's index; null when unlisted.</summary>
    public string? DisplayName() => SaveIndex.GetName(System.IO.Path.GetDirectoryName(Path)!, ProfileKey, SaveId);

    public int Currency(string assetName) => Inventory.Total(assetName);

    /// <summary>Writes the save. Originals become a new file listed in the load menu as <paramref name="displayName"/>.</summary>
    public SaveOutcome Save(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var folder = System.IO.Path.GetDirectoryName(Path)!;
        // Overwrite only when the file on disk right now is one this editor wrote.
        var createNew = IsOriginalGameSave || !DiskFileIsEditorSave(Path);
        var sourceId = SaveId;
        var target = createNew
            ? System.IO.Path.Combine(folder, $"{ProfileKey}_{Guid.NewGuid():N}".ToUpperInvariant() + ".sav")
            : Path;

        Commit();
        EditorStamp.Apply(_file.Properties, ToolName, Version, sourceId, ue5: true);
        var bytes = _file.ToFileBytes();
        Ue5SaveFile.Parse(bytes); // refuse to write anything that doesn't read back

        if (!createNew)
        {
            File.Copy(target, target + ".bak", overwrite: true);
        }

        var temp = target + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, target, overwrite: true);

        var summary = new SaveSummary(Currency("Kerenskies_CurrencyItem"), Currency("Merits_CurrencyItem"), Currency("Honor_CurrencyItem"));
        var newId = System.IO.Path.GetFileNameWithoutExtension(target).Split('_')[1];
        Path = target;
        IsOriginalGameSave = false;
        bool registered;
        try
        {
            registered = SaveIndex.Register(folder, ProfileKey, createNew ? sourceId : newId, newId, displayName, summary);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The save itself is already on disk; a locked or read-only index must not report it as unwritten.
            registered = false;
        }

        return new SaveOutcome(target, createNew, registered);
    }

    private static string Version =>
        typeof(ClansDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1";

    /// <summary>True when <paramref name="path"/> exists and carries this family's editor stamp.</summary>
    public static bool DiskFileIsEditorSave(string path)
    {
        try
        {
            return File.Exists(path) && EditorStamp.Read(Ue5SaveFile.Load(path).Properties) is not null;
        }
        catch (SaveFormatException)
        {
            return false;
        }
    }

    private (ByteArrayStorage, Ue5ObjectBlob) Expand(string name)
    {
        var storage = _file.Properties.Get<ArrayProperty>(name).Bytes;
        return (storage, Ue5ObjectBlob.Read(storage.Data));
    }

    private void Commit()
    {
        Inventory.Commit();
        _inventory.Storage.Data = _inventory.Blob.ToArray();
        _models.Storage.Data = _models.Blob.ToArray();
        if (_deployment is { } deployment)
        {
            deployment.Storage.Data = deployment.Blob.ToArray();
        }
    }

    /// <summary>Whether anything outside the inventory (deployment, models) mentions this inventory id.</summary>
    private bool IsReferenced(Guid id)
    {
        var needle = id.ToByteArray();
        _models.Storage.Data = _models.Blob.ToArray();
        if (_models.Storage.Data.AsSpan().IndexOf(needle) >= 0)
        {
            return true;
        }

        if (_deployment is { } deployment)
        {
            deployment.Storage.Data = deployment.Blob.ToArray();
            return deployment.Storage.Data.AsSpan().IndexOf(needle) >= 0;
        }

        return false;
    }
}
