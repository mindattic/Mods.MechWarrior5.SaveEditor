using System.Buffers.Binary;

namespace MechWarrior5.Clans.SaveEditor.Core.Saves;

/// <summary>A campaign save as the load menu describes it.</summary>
public sealed record SaveEntry(
    string Path,
    string ProfileKey,
    string SaveId,
    string Name,
    string Campaign,
    DateTime SavedUtc,
    int Kerenskies,
    bool IsAutoSave,
    string SaveType = "ManualSave",
    bool IsMissionSave = false);

/// <summary>Lists the Clans saves in a folder using the game's own load-menu index.</summary>
public static class SaveLibrary
{
    public static string DefaultFolder =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MW5Clans", "Saved", "SaveGames");

    /// <summary>Every campaign save in <paramref name="folder"/>, newest first.</summary>
    public static IReadOnlyList<SaveEntry> List(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var headers = ReadHeaders(folder);
        var entries = new List<SaveEntry>();
        foreach (var path in Directory.EnumerateFiles(folder, "*_*.sav"))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var parts = name.Split('_');
            if (parts.Length != 2)
            {
                continue;
            }

            headers.TryGetValue(parts[1], out var header);
            entries.Add(new SaveEntry(
                path,
                parts[0],
                parts[1],
                header?.Name is { Length: > 0 } title ? title : header is { IsAutoSave: true } ? "Autosave" : parts[1],
                header?.Campaign ?? string.Empty,
                header?.SavedUtc ?? File.GetLastWriteTimeUtc(path),
                header?.Kerenskies ?? 0,
                header?.IsAutoSave ?? false,
                header?.SaveType ?? "ManualSave",
                header?.IsMissionSave ?? false));
        }

        return [.. entries.OrderByDescending(e => e.SavedUtc)];
    }

    private sealed record HeaderInfo(string? Name, string? Campaign, DateTime SavedUtc, int Kerenskies, bool IsAutoSave, string SaveType, bool IsMissionSave);

    private static Dictionary<string, HeaderInfo> ReadHeaders(string folder)
    {
        var result = new Dictionary<string, HeaderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in SaveIndex.Files(folder))
        {
            Ue5SaveFile index; // files come most authoritative first; earlier entries win
            try
            {
                index = Ue5SaveFile.Load(file);
            }
            catch (SaveFormatException)
            {
                continue;
            }

            if (index.Properties.Find<ArrayProperty>("UserProfiles")?.Storage is not StructArrayStorage profiles)
            {
                continue;
            }

            foreach (var profile in profiles.PropertyElements)
            {
                var campaign = (profile.Find<StructProperty>("ProfileInfo")?.Value as PropertyList)?.Find<TextProperty>("Name")?.DisplayString;
                if (profile.Find<ArrayProperty>("SaveHeaders")?.Storage is not StructArrayStorage saves)
                {
                    continue;
                }

                foreach (var header in saves.PropertyElements)
                {
                    if (header.Find<StrProperty>("SaveFileName")?.Value.Value is not { } id || result.ContainsKey(id))
                    {
                        continue;
                    }

                    var ticks = header.Find<StructProperty>("SaveDate")?.Value is RawStructValue { Data.Length: 8 } raw
                        ? BinaryPrimitives.ReadInt64LittleEndian(raw.Data)
                        : 0;
                    result[id] = new HeaderInfo(
                        header.Find<TextProperty>("Name")?.DisplayString,
                        campaign,
                        ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue,
                        header.Find<IntProperty>("Kerenskies")?.Value ?? 0,
                        header.Find<EnumProperty>("SaveType")?.Value.Value?.EndsWith("AutoSave", StringComparison.Ordinal) == true,
                        Ue.EnumValue(header.Find<EnumProperty>("SaveType")?.Value ?? FString.From("ManualSave")),
                        header.Find<BoolProperty>("bIsMissionSave")?.Value == true);
                }
            }
        }

        return result;
    }
}
