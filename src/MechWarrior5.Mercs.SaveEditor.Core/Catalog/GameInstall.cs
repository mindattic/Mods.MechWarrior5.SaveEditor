using System.Text.Json;
using System.Text.RegularExpressions;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

/// <summary>Finds the MechWarrior 5: Mercenaries install (Steam libraries, Epic manifests, common folders).</summary>
public static partial class GameInstall
{
    public const string PakRelative = @"MW5Mercs\Content\Paks\MW5Mercs-WindowsNoEditor.pak";
    public const string ModsRelative = @"MW5Mercs\Mods";

    public static string? Find(string? preferred = null)
    {
        foreach (var candidate in Candidates(preferred))
        {
            if (File.Exists(Path.Combine(candidate, PakRelative)))
            {
                return candidate;
            }
        }

        return null;
    }

    public const string ClansPaksRelative = @"MW5Clans\Content\Paks";

    /// <summary>The MechWarrior 5: Clans install (its IoStore paks), for reading its own 'Mech art.</summary>
    public static string? FindClans()
    {
        var candidates = SteamCommonFolders().Select(c => Path.Combine(c, "MechWarrior 5 Clans"))
            .Concat(EpicInstallLocations().Where(l => l.Contains("Clans", StringComparison.OrdinalIgnoreCase)));
        foreach (var candidate in candidates)
        {
            var paks = Path.Combine(candidate, ClansPaksRelative);
            if (Directory.Exists(paks) && Directory.EnumerateFiles(paks, "*.utoc").Any())
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Every Steam library's <c>steamapps\common</c> folder that exists.</summary>
    public static IEnumerable<string> SteamCommonFolders() =>
        SteamLibraries()
            .Concat(DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).SelectMany(d => new[]
            {
                Path.Combine(d.RootDirectory.FullName, "Program Files (x86)", "Steam"),
                Path.Combine(d.RootDirectory.FullName, "SteamLibrary"),
                Path.Combine(d.RootDirectory.FullName, "Games", "Steam"),
            }))
            .Select(l => Path.Combine(l, "steamapps", "common"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);

    /// <summary>Every Epic Games Launcher install location (games and Unreal Engine versions).</summary>
    public static IEnumerable<string> EpicInstallLocations()
    {
        var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifests))
        {
            yield break;
        }

        foreach (var item in Directory.EnumerateFiles(manifests, "*.item"))
        {
            string? location = null;
            try
            {
                using var doc = JsonDocument.Parse(SafeRead(item));
                location = doc.RootElement.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
            }
            catch (JsonException)
            {
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                yield return location;
            }
        }
    }

    private static IEnumerable<string> Candidates(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            yield return preferred;
        }

        foreach (var library in SteamLibraries())
        {
            yield return Path.Combine(library, "steamapps", "common", "MechWarrior 5 Mercenaries");
        }

        foreach (var epic in EpicInstalls())
        {
            yield return epic;
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            yield return Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "Steam", "steamapps", "common", "MechWarrior 5 Mercenaries");
            yield return Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "MechWarrior 5 Mercenaries");
            yield return Path.Combine(drive.RootDirectory.FullName, "Games", "Steam", "steamapps", "common", "MechWarrior 5 Mercenaries");
            yield return Path.Combine(drive.RootDirectory.FullName, "Program Files", "Epic Games", "MW5Mercs");
        }
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        }.Select(p => Path.Combine(p, "Steam"));
        foreach (var root in roots)
        {
            yield return root;
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
            {
                continue;
            }

            foreach (Match m in VdfPath().Matches(SafeRead(vdf)))
            {
                yield return m.Groups[1].Value.Replace(@"\\", @"\", StringComparison.Ordinal);
            }
        }
    }

    private static IEnumerable<string> EpicInstalls() =>
        EpicInstallLocations().Where(l => l.Contains("MW5", StringComparison.OrdinalIgnoreCase));

    private static string SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex VdfPath();
}
