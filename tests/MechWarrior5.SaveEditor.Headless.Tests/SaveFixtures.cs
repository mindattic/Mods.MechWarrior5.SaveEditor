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
            .Where(c => c.Saves.Any(s => !s.IsAutosave && !Mercs.SaveEditor.Core.Saves.EditorStamp.FileHasStamp(s.Path)))
            .OrderByDescending(c => c.Saves.Count)
            .FirstOrDefault();
        if (campaign is null)
        {
            Assert.Ignore($"No MW5 Mercenaries campaign with a manual save under {MercsRoot}.");
        }

        var target = Sandbox("mercs");
        CopyFolder(campaign!.Folder, target);
        // Newest original manual save: editor saves are updated in place, which these tests don't want.
        var save = campaign.Saves
            .Where(s => !s.IsAutosave && !Mercs.SaveEditor.Core.Saves.EditorStamp.FileHasStamp(s.Path))
            .OrderByDescending(s => s.LastWrite)
            .First();
        return (target, Path.Combine(target, Path.GetFileName(save.Path)));
    }

    /// <summary>A sandbox copy of the Clans save folder and its newest manual save.</summary>
    public static (string Folder, string Save) ClansFolderCopy()
    {
        AllClansFiles();
        var target = Sandbox("clans");
        CopyFolder(ClansRoot, target);
        var saves = Clans.SaveEditor.Core.Saves.SaveLibrary.List(target);
        var save = saves.FirstOrDefault(s => !s.IsAutoSave && !Clans.SaveEditor.Core.Game.ClansDocument.DiskFileIsEditorSave(s.Path)) ?? saves[0];
        return (target, save.Path);
    }

    /// <summary>
    /// A sandbox holding one original (game-written) Mercenaries save of <paramref name="kind"/> plus its
    /// Campaign.json. Kinds: "Manual", or the game's autosave names, e.g. "AutoSave (Entered System)".
    /// </summary>
    public static (string Folder, string Save) MercsSaveOfKind(string kind)
    {
        var match = CampaignLibrary.Scan(MercsRoot)
            .SelectMany(c => c.Saves.Select(s => (Campaign: c, Save: s)))
            .Where(x => kind == "Manual" ? !x.Save.IsAutosave : x.Save.Name == kind)
            .Where(x => !Mercs.SaveEditor.Core.Saves.EditorStamp.FileHasStamp(x.Save.Path))
            .OrderByDescending(x => x.Save.LastWrite)
            .FirstOrDefault();
        if (match.Save is null)
        {
            Assert.Ignore($"No original MW5 Mercenaries save of kind '{kind}' under {MercsRoot}.");
        }

        var target = Sandbox("mercs");
        File.Copy(match.Save!.Path, Path.Combine(target, Path.GetFileName(match.Save.Path)));
        File.Copy(Path.Combine(match.Campaign.Folder, "Campaign.json"), Path.Combine(target, "Campaign.json"));
        return (target, Path.Combine(target, Path.GetFileName(match.Save.Path)));
    }

    /// <summary>
    /// A sandbox holding one original Clans save of <paramref name="kind"/> plus the load-menu indexes.
    /// Kinds: "ManualSave", "AutoSave", "AutoSave (mission)", "ReloadSave".
    /// </summary>
    public static (string Folder, string Save) ClansSaveOfKind(string kind)
    {
        AllClansFiles();
        var type = kind.Split(' ')[0];
        var mission = kind.Contains("mission", StringComparison.Ordinal) || type == "ReloadSave";
        var match = Clans.SaveEditor.Core.Saves.SaveLibrary.List(ClansRoot)
            .Where(s => s.SaveType == type && s.IsMissionSave == mission)
            .FirstOrDefault(s => !Clans.SaveEditor.Core.Game.ClansDocument.DiskFileIsEditorSave(s.Path));
        if (match is null)
        {
            Assert.Ignore($"No original MW5 Clans save of kind '{kind}' under {ClansRoot}.");
        }

        var target = Sandbox("clans");
        File.Copy(match!.Path, Path.Combine(target, Path.GetFileName(match.Path)));
        foreach (var index in Directory.EnumerateFiles(ClansRoot, "UserSaveMetaData*.sav"))
        {
            File.Copy(index, Path.Combine(target, Path.GetFileName(index)));
        }

        return (target, Path.Combine(target, Path.GetFileName(match.Path)));
    }

    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static void CleanUp()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWarrior5.SaveEditor.Headless", TestContext.CurrentContext.Test.ID);
        if (Directory.Exists(root))
        {
            DeleteTree(root);
        }
    }

    internal static string Sandbox(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "MechWarrior5.SaveEditor.Headless", TestContext.CurrentContext.Test.ID, name);
        if (Directory.Exists(dir))
        {
            DeleteTree(dir);
        }

        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Deletes a sandbox even when a test left read-only files in it.</summary>
    private static void DeleteTree(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(dir, recursive: true);
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }
}
