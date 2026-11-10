using System.Security.Cryptography;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Real saves, copied into a throwaway sandbox per test. The originals are only ever read; every
/// edit, save and backup happens in the copy.
/// </summary>
internal static class SaveFixtures
{
    public static string MercsRoot =>
        Environment.GetEnvironmentVariable("MW5_SAVE_DIR") is { Length: > 0 } dir ? dir : CampaignLibrary.DefaultSaveRoot;

    public static string ClansRoot =>
        Environment.GetEnvironmentVariable("MW5_CLANS_SAVE_DIR") is { Length: > 0 } dir
            ? dir
            : Clans.SaveEditor.Core.Saves.SaveLibrary.DefaultFolder;

    /// <summary>Every Mercenaries campaign save on this machine (read-only use).</summary>
    public static IReadOnlyList<string> AllMercsSaves()
    {
        var campaigns = CampaignLibrary.Scan(MercsRoot);
        if (campaigns.Count == 0)
        {
            Assert.Ignore($"No MW5 Mercenaries saves under {MercsRoot}.");
        }

        return [.. campaigns.SelectMany(c => c.Saves).Select(s => s.Path)];
    }

    /// <summary>Every Clans campaign save and index file on this machine (read-only use).</summary>
    public static IReadOnlyList<string> AllClansFiles()
    {
        if (!Directory.Exists(ClansRoot) || !Directory.EnumerateFiles(ClansRoot, "*_*.sav").Any())
        {
            Assert.Ignore($"No MW5 Clans saves under {ClansRoot}.");
        }

        return [.. Directory.EnumerateFiles(ClansRoot, "*.sav")];
    }

    /// <summary>
    /// A sandbox copy of the Mercenaries campaign with the most saves; returns the copied folder and its
    /// newest non-autosave save, so tests run against a realistic, well-developed company.
    /// </summary>
    public static (string Folder, string Save) MercsCampaignCopy()
    {
        var campaign = CampaignLibrary.Scan(MercsRoot)
            .Where(c => c.Saves.Any(s => !s.IsAutosave))
            .OrderByDescending(c => c.Saves.Count)
            .FirstOrDefault();
        if (campaign is null)
        {
            Assert.Ignore($"No MW5 Mercenaries campaign with a manual save under {MercsRoot}.");
        }

        var target = Sandbox("mercs");
        CopyFolder(campaign!.Folder, target);
        var save = campaign.Saves.Where(s => !s.IsAutosave).OrderByDescending(s => s.LastWrite).First();
        return (target, Path.Combine(target, Path.GetFileName(save.Path)));
    }

    /// <summary>A sandbox copy of the Clans save folder and its newest manual save.</summary>
    public static (string Folder, string Save) ClansFolderCopy()
    {
        AllClansFiles();
        var target = Sandbox("clans");
        CopyFolder(ClansRoot, target);
        var saves = Clans.SaveEditor.Core.Saves.SaveLibrary.List(target);
        var save = saves.FirstOrDefault(s => !s.IsAutoSave) ?? saves[0];
        return (target, save.Path);
    }

    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static void CleanUp()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWarrior5.SaveEditor.Headless", TestContext.CurrentContext.Test.ID);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Sandbox(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "MechWarrior5.SaveEditor.Headless", TestContext.CurrentContext.Test.ID, name);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }
}
