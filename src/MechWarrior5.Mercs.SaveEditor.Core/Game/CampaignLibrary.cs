using System.Globalization;
using System.Text.Json;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

public sealed record CampaignInfo(string Folder, string Name, string? CompanyGameDate, IReadOnlyList<SaveInfo> Saves);

public sealed record SaveInfo(
    string Path,
    string Name,
    string? GameDate,
    string? StarSystem,
    int? Reputation,
    DateTime LastWrite,
    bool IsAutosave,
    int DlcCount,
    bool IsEditorSave = false);

/// <summary>Finds campaigns under the MW5 save folder and names their GUID-named saves.</summary>
public static class CampaignLibrary
{
    public static string DefaultSaveRoot { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MW5Mercs", "Saved", "SaveGames");

    public static IReadOnlyList<CampaignInfo> Scan(string? root = null)
    {
        root ??= DefaultSaveRoot;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var campaigns = new List<CampaignInfo>();
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            if (Read(folder) is { Saves.Count: > 0 } campaign)
            {
                campaigns.Add(campaign);
            }
        }

        return campaigns.OrderByDescending(c => c.Saves.Max(s => s.LastWrite)).ToList();
    }

    private static CampaignInfo? Read(string folder)
    {
        var index = System.IO.Path.Combine(folder, "Campaign.json");
        var files = Directory.EnumerateFiles(folder, "*.sav").ToDictionary(
            f => System.IO.Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase);
        if (files.Count == 0)
        {
            return null;
        }

        var saves = new List<SaveInfo>();
        var name = System.IO.Path.GetFileName(folder);
        string? gameDate = null;
        if (File.Exists(index))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(index));
                var root = doc.RootElement;
                name = Str(root, "Name") ?? name;
                gameDate = FormatGameDate(Str(root, "GameDate"));
                if (root.TryGetProperty("Saves", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var save in list.EnumerateArray())
                    {
                        var file = Str(save, "SaveFileName");
                        if (file is null || !files.Remove(file, out var path))
                        {
                            continue;
                        }

                        saves.Add(new SaveInfo(
                            path,
                            Str(save, "Name") ?? file,
                            FormatGameDate(Str(save, "GameDate")),
                            Str(save, "StarSystemName"),
                            save.TryGetProperty("Reputation", out var rep) && rep.TryGetInt32(out var r) ? r : null,
                            File.GetLastWriteTime(path),
                            save.TryGetProperty("SaveType", out var type) && type.TryGetInt32(out var t) && t != 0,
                            save.TryGetProperty("DLCTags", out var tags) && tags.ValueKind == JsonValueKind.Array
                                ? tags.GetArrayLength()
                                : 0,
                            Saves.EditorStamp.FileHasStamp(path)));
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        // Saves the index doesn't know about (e.g. renamed copies) still appear, by file name.
        saves.AddRange(files.Values
            .Where(IsCampaignSaveFile)
            .Select(p => new SaveInfo(p, System.IO.Path.GetFileNameWithoutExtension(p), null, null, null, File.GetLastWriteTime(p), false, 0,
                Saves.EditorStamp.FileHasStamp(p))));
        return new CampaignInfo(folder, name, gameDate, saves.OrderByDescending(s => s.LastWrite).ToList());
    }

    private static bool IsCampaignSaveFile(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        return !name.StartsWith("blocked_contacts", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("saved_contacts", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>MW5 stores dates as <c>3015.10.10-14.00.00</c>.</summary>
    private static string? FormatGameDate(string? raw) =>
        raw is not null && DateTime.TryParseExact(raw, "yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : raw;
}
