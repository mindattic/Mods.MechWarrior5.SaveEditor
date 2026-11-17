using System.IO;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Shared.Assets;

namespace MechWarrior5.SaveEditor.App.Services;

/// <summary>
/// Finds an Oodle DLL (<c>oo2core*.dll</c>) already on this PC: the one picked in Settings, else one inside an Unreal
/// Engine install (Epic Games Launcher) or a Steam game. Local files only; nothing is downloaded.
/// </summary>
public static class OodleLocator
{
    private static readonly EnumerationOptions Shallow = new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = 4,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    private static readonly EnumerationOptions Deep = new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = 12,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    public static Oodle? Find(string? preferred)
    {
        foreach (var candidate in Candidates(preferred))
        {
            if (Oodle.TryLoad(candidate, out _) is { } oodle)
            {
                return oodle;
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

        // Unreal Engine ships Oodle with its tools (Engine\Binaries\...).
        foreach (var location in GameInstall.EpicInstallLocations())
        {
            var binaries = Path.Combine(location, "Engine", "Binaries");
            foreach (var dll in Search(binaries, Deep))
            {
                yield return dll;
            }
        }

        // Many games ship oo2core_N_win64.dll beside their exe.
        foreach (var common in GameInstall.SteamCommonFolders())
        {
            foreach (var game in SafeDirectories(common))
            {
                foreach (var dll in Search(game, Shallow).Where(d => d.Contains("win64", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(d).Equals("oo2core.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return dll;
                }
            }
        }
    }

    private static IEnumerable<string> Search(string folder, EnumerationOptions options)
    {
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "oo2core*.dll", options)] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeDirectories(string folder)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(folder)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
