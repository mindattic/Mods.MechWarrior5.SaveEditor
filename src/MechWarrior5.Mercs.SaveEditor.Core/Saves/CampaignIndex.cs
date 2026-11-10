using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MechWarrior5.Mercs.SaveEditor.Core.Saves;

/// <summary>
/// Edits a campaign's <c>Campaign.json</c>, the index the game's load menu reads. Saves written as
/// new files are registered here by cloning the source save's entry, so they show up in game.
/// </summary>
public static class CampaignIndex
{
    public const string EditedSuffix = " [Edited]";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The load-menu name of a save, or null when the index doesn't list it.</summary>
    public static string? GetName(string folder, string fileName) =>
        TryLoad(folder, out var root, out _) && root["Saves"] is JsonArray saves
            ? saves.OfType<JsonObject>().FirstOrDefault(s => Matches(s, fileName))?["Name"]?.GetValue<string>()
            : null;

    /// <summary>Updates a save's load-menu name and/or game date; returns false when the index doesn't list it.</summary>
    public static bool Update(string folder, string fileName, string? name, DateTime? gameDate)
    {
        if (!TryLoad(folder, out var root, out var hasBom) || root["Saves"] is not JsonArray saves
            || saves.OfType<JsonObject>().FirstOrDefault(s => Matches(s, fileName)) is not { } entry)
        {
            return false;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(name) && entry["Name"]?.GetValue<string>() != name.Trim())
        {
            entry["Name"] = name.Trim();
            changed = true;
        }

        if (gameDate is { } date && entry["GameDate"]?.GetValue<string>() != FormatDate(date))
        {
            entry["GameDate"] = FormatDate(date);
            changed = true;
        }

        if (changed)
        {
            Write(folder, root, hasBom);
        }

        return true;
    }

    /// <summary>MW5's own index format, e.g. <c>3015.10.10-14.00.00</c>.</summary>
    public static string FormatDate(DateTime date) =>
        date.ToString("yyyy.MM.dd-HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Lists a newly written save by cloning its source save's entry under <paramref name="displayName"/>
    /// (default: the source name plus " [Edited]"). Returns false when there is no index or entry to clone.
    /// </summary>
    public static bool RegisterCopy(string folder, string sourceFileName, string newFileName, string? displayName = null, DateTime? gameDate = null)
    {
        if (!TryLoad(folder, out var root, out var hasBom) || root["Saves"] is not JsonArray saves)
        {
            return false;
        }

        var source = saves.OfType<JsonObject>().FirstOrDefault(s => Matches(s, sourceFileName))
            ?? saves.OfType<JsonObject>().FirstOrDefault(s => Matches(s, root["LastSaveFileName"]?.GetValue<string>()));
        if (source is null)
        {
            return false;
        }

        if (saves.OfType<JsonObject>().Any(s => Matches(s, newFileName)))
        {
            return true;
        }

        var copy = (JsonObject)source.DeepClone();
        copy["SaveFileName"] = newFileName;
        copy["Name"] = string.IsNullOrWhiteSpace(displayName)
            ? DefaultCopyName(copy["Name"]?.GetValue<string>() ?? newFileName)
            : displayName.Trim();
        copy["SaveType"] = 0;
        if (gameDate is { } date)
        {
            copy["GameDate"] = FormatDate(date);
        }

        saves.Insert(0, copy);
        Write(folder, root, hasBom);
        return true;
    }

    public static string DefaultCopyName(string sourceName) =>
        sourceName.EndsWith(EditedSuffix, StringComparison.Ordinal) ? sourceName : sourceName + EditedSuffix;

    private static bool TryLoad(string folder, out JsonObject root, out bool hasBom)
    {
        root = null!;
        hasBom = false;
        var path = Path.Combine(folder, "Campaign.json");
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            if (JsonNode.Parse(bytes.AsSpan(hasBom ? 3 : 0)) is JsonObject obj)
            {
                root = obj;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    /// <summary>Writes atomically, keeping the previous index as <c>Campaign.json.bak</c>.</summary>
    private static void Write(string folder, JsonObject root, bool hasBom)
    {
        var path = Path.Combine(folder, "Campaign.json");
        File.Copy(path, path + ".bak", overwrite: true);
        var text = Encoding.UTF8.GetBytes(root.ToJsonString(Json));
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, hasBom ? [.. Encoding.UTF8.Preamble, .. text] : text);
        File.Move(temp, path, overwrite: true);
    }

    private static bool Matches(JsonObject save, string? fileName) =>
        fileName is not null
        && string.Equals(save["SaveFileName"]?.GetValue<string>(), fileName, StringComparison.OrdinalIgnoreCase);
}
