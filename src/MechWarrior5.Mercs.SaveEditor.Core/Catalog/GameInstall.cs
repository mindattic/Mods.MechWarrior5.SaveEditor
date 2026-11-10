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

    private static IEnumerable<string> EpicInstalls()
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

            if (location is not null && location.Contains("MW5", StringComparison.OrdinalIgnoreCase))
            {
                yield return location;
            }
        }
    }

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
