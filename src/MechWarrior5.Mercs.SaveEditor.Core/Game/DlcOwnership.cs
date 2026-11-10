using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

/// <summary>
/// The DLC packs a save was made with, read from the <c>Campaign.json</c> beside it. A save the
/// index does not list (e.g. one renamed by hand) inherits the campaign's latest save's packs.
/// </summary>
public sealed class DlcOwnership
{
    private readonly HashSet<string> _tags;

    private DlcOwnership(IEnumerable<string> tags, DlcSource source)
    {
        _tags = new HashSet<string>(tags, StringComparer.Ordinal);
        Source = source;
    }

    public DlcSource Source { get; }

    public IReadOnlyCollection<string> Tags => _tags;

    public bool Owns(DlcPack? pack) => pack is null || _tags.Contains(pack.Tag);

    public static DlcOwnership ForSave(string savePath)
    {
        var index = Path.Combine(Path.GetDirectoryName(savePath) ?? ".", "Campaign.json");
        if (!File.Exists(index))
        {
            return new DlcOwnership([], DlcSource.Unknown);
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(index));
            var root = doc.RootElement;
            if (!root.TryGetProperty("Saves", out var saves) || saves.ValueKind != JsonValueKind.Array)
            {
                return new DlcOwnership([], DlcSource.Unknown);
            }

            var name = Path.GetFileNameWithoutExtension(savePath);
            if (FindSave(saves, name) is { } exact)
            {
                return new DlcOwnership(TagsOf(exact), DlcSource.ThisSave);
            }

            var latest = root.TryGetProperty("LastSaveFileName", out var last) ? last.GetString() : null;
            if (latest is not null && FindSave(saves, latest) is { } recent)
            {
                return new DlcOwnership(TagsOf(recent), DlcSource.CampaignLatestSave);
            }

            return new DlcOwnership([], DlcSource.Unknown);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new DlcOwnership([], DlcSource.Unknown);
        }
    }

    private static JsonElement? FindSave(JsonElement saves, string fileName)
    {
        foreach (var save in saves.EnumerateArray())
        {
            if (save.TryGetProperty("SaveFileName", out var n)
                && string.Equals(n.GetString(), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return save;
            }
        }

        return null;
    }

    private static List<string> TagsOf(JsonElement? save) =>
        save is { } s && s.TryGetProperty("DLCTags", out var tags) && tags.ValueKind == JsonValueKind.Array
            ? tags.EnumerateArray().Select(t => t.GetString()).OfType<string>().ToList()
            : [];
}

public enum DlcSource
{
    /// <summary>No campaign index was found; DLC mechs are allowed but unverified.</summary>
    Unknown,
    ThisSave,
    CampaignLatestSave,
}
