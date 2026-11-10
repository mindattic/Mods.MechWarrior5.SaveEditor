using System.Buffers.Binary;

namespace MechWarrior5.Clans.SaveEditor.Core.Saves;

/// <summary>The currency totals the load menu shows for a save.</summary>
public sealed record SaveSummary(int Kerenskies, int Merits, int Honor);

/// <summary>
/// The game's load-menu index: <c>UserSaveMetaData*.sav</c> (written round-robin), each holding
/// per-profile <c>SaveHeaders</c>. A save only shows in the load menu when it has a header here.
/// </summary>
public static class SaveIndex
{
    /// <summary>
    /// The index files, most authoritative first: the game rotates through them, and the one with
    /// the highest <c>SaveCounter</c> is the latest it wrote. Unreadable files come last.
    /// </summary>
    public static IEnumerable<string> Files(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "UserSaveMetaData*.sav")
                .Select(f => (File: f, Counter: Counter(f)))
                .OrderByDescending(x => x.Counter)
                .ThenByDescending(x => File.GetLastWriteTimeUtc(x.File))
                .Select(x => x.File)
                .ToList()
            : [];

    private static int Counter(string file)
    {
        try
        {
            return Ue5SaveFile.Load(file).Properties.Find<IntProperty>("SaveCounter")?.Value ?? 0;
        }
        catch (Exception e) when (e is SaveFormatException or IOException)
        {
            return -1;
        }
    }

    /// <summary>The load-menu name of a save from the newest index file; null when unlisted.</summary>
    public static string? GetName(string folder, string profileKey, string saveId)
    {
        foreach (var file in Files(folder))
        {
            try
            {
                if (FindHeader(Ue5SaveFile.Load(file), profileKey, saveId) is { } header)
                {
                    return header.Find<TextProperty>("Name")?.DisplayString;
                }
            }
            catch (SaveFormatException)
            {
            }
        }

        return null;
    }

    /// <summary>
    /// Lists <paramref name="newId"/> in every index file: an existing header is renamed and
    /// refreshed, otherwise the header of <paramref name="sourceId"/> is copied. Each index file is
    /// backed up to <c>.bak</c> first. Returns whether any index now lists the save.
    /// </summary>
    public static bool Register(string folder, string profileKey, string sourceId, string newId, string displayName, SaveSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var registered = false;
        foreach (var file in Files(folder).ToList())
        {
            Ue5SaveFile index;
            try
            {
                index = Ue5SaveFile.Load(file);
            }
            catch (SaveFormatException)
            {
                continue;
            }

            if (Headers(index, profileKey) is not { } headers)
            {
                continue;
            }

            var header = headers.PropertyElements.FirstOrDefault(h => SaveFileName(h) == newId);
            if (header is null)
            {
                if (headers.PropertyElements.FirstOrDefault(h => SaveFileName(h) == sourceId) is not { } source)
                {
                    continue;
                }

                header = source.DeepClone();
                header.Get<StrProperty>("SaveFileName").Value = newId;
                if (header.Find<EnumProperty>("SaveType") is { } type)
                {
                    type.Value = "EMWSaveType::ManualSave";
                }

                if (header.Find<BoolProperty>("bIsMissionSave") is { } mission)
                {
                    mission.Value = false;
                }

                headers.Elements.Add(header);
            }

            if (header.Find<TextProperty>("Name") is { } name)
            {
                name.Data = FText.WithDisplayString(name.Data, displayName);
            }

            SetInt(header, "Kerenskies", summary.Kerenskies);
            SetInt(header, "Merits", summary.Merits);
            SetInt(header, "Honor", summary.Honor);
            if (header.Find<StructProperty>("SaveDate") is { Value: RawStructValue date } && date.Data.Length == 8)
            {
                BinaryPrimitives.WriteInt64LittleEndian(date.Data, DateTime.UtcNow.Ticks);
            }

            var bytes = index.ToFileBytes();
            Ue5SaveFile.Parse(bytes);
            File.Copy(file, file + ".bak", overwrite: true);
            File.WriteAllBytes(file, bytes);
            registered = true;
        }

        return registered;
    }

    private static PropertyList? FindHeader(Ue5SaveFile index, string profileKey, string saveId) =>
        Headers(index, profileKey)?.PropertyElements.FirstOrDefault(h => SaveFileName(h) == saveId);

    private static StructArrayStorage? Headers(Ue5SaveFile index, string profileKey) =>
        index.Properties.Find<ArrayProperty>("UserProfiles")?.Storage is StructArrayStorage profiles
            ? profiles.PropertyElements
                .FirstOrDefault(p => p.Find<StrProperty>("ProfileKey")?.Value.Value == profileKey)
                ?.Find<ArrayProperty>("SaveHeaders")?.Storage as StructArrayStorage
            : null;

    private static string? SaveFileName(PropertyList header) => header.Find<StrProperty>("SaveFileName")?.Value.Value;

    private static void SetInt(PropertyList list, string name, int value)
    {
        if (list.Find<IntProperty>(name) is { } existing)
        {
            existing.Value = value;
        }
        else if (value != 0)
        {
            list.Add(new IntProperty(name, value) { Ue5 = Ue5Tag.Simple("IntProperty") });
        }
    }
}
