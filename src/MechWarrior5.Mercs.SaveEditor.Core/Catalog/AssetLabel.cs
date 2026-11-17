using System.Text.RegularExpressions;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

/// <summary>Readable labels for asset ids, e.g. <c>LBXAutocannon10_Lvl2</c> → <c>LBX Autocannon 10</c>.</summary>
public static partial class AssetLabel
{
    public static string Humanize(string asset)
    {
        var name = TierSuffix().Replace(asset, string.Empty);
        name = name.Replace("_AmmoEquipment", " Ammo", StringComparison.Ordinal)
            .Replace("_Equipment", string.Empty, StringComparison.Ordinal)
            .Replace('_', ' ');
        name = WordBreak().Replace(name, " ");
        name = MultiSpace().Replace(name, " ").Trim();

        // Small words stay lowercase unless they lead: "Magistracy of Canopus", not "Magistracy Of Canopus".
        var words = name.Split(' ');
        for (var i = 1; i < words.Length; i++)
        {
            if (words[i] is "Of" or "The" or "And")
            {
                words[i] = words[i].ToLowerInvariant();
            }
        }

        return string.Join(' ', words);
    }

    /// <summary>A trait's label from its asset id, without the <c>_PilotTrait</c> / <c>_Trait</c> suffix.</summary>
    public static string HumanizeTrait(string asset) =>
        Humanize(TraitSuffix().Replace(asset, string.Empty));

    /// <summary>
    /// Game text with its runtime placeholders (<c>{InjuryRecoveryAdjPct}</c>, <c>{0}</c>) shown as "X", since the
    /// editor has no values to put there; null for blank text.
    /// </summary>
    public static string? CleanText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Placeholder().Replace(text.Trim(), "X");

    /// <summary>A display name from game text: null when blank or templated (<c>{MechType} Mastery</c>).</summary>
    public static string? GameName(string? text) =>
        string.IsNullOrWhiteSpace(text) || text.Contains('{', StringComparison.Ordinal) ? null : text.Trim();

    [GeneratedRegex(@"_Lvl(\d+)$")]
    private static partial Regex TierSuffix();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z][a-z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBreak();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"_(Pilot)?Trait$", RegexOptions.IgnoreCase)]
    private static partial Regex TraitSuffix();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Placeholder();
}
