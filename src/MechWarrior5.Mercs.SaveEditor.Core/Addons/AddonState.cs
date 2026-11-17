using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.Mercs.SaveEditor.Core.Addons;

public enum AddonStatus
{
    Active,
    TurnedOff,
    AllTurnedOff,
    ModNotInstalled,
    ModDisabled,
    VersionNotSupported,
    NeedsPack,
    NotForThisGame,
    Invalid,
}

/// <summary>One pack file and what became of it: active, or the reason it isn't.</summary>
public sealed record AddonEntry(string File, AddonPack? Pack, AddonStatus Status, string? Detail, InstalledMod? Mod)
{
    public string Name => Pack?.Name ?? File;
}

/// <summary>
/// The single decision point for add-ons: which packs apply (their mod installed, enabled and a listed version, not
/// turned off, every pack they need active too), in the game's mod load order, and the limits they add up to. The
/// limits also take in what the game data itself says: the base game's skill curve, and an enabled mod's curve.
/// </summary>
public sealed class AddonState
{
    private AddonState(IReadOnlyList<AddonEntry> entries, IReadOnlyList<AddonPack> active, GameLimits limits,
        IReadOnlyDictionary<string, string> limitSources, IReadOnlyList<string> collisions)
    {
        Entries = entries;
        Active = active;
        Limits = limits;
        LimitSources = limitSources;
        Collisions = collisions;
    }

    /// <summary>No packs and no game data: vanilla.</summary>
    public static AddonState None { get; } = new([], [], GameLimits.Vanilla, new Dictionary<string, string>(), []);

    public IReadOnlyList<AddonEntry> Entries { get; }

    /// <summary>Active packs, in mod load order (a later one wins where two change the same thing).</summary>
    public IReadOnlyList<AddonPack> Active { get; }

    public GameLimits Limits { get; }

    /// <summary>Which mod or pack widened each limit beyond vanilla, by limit key.</summary>
    public IReadOnlyDictionary<string, string> LimitSources { get; }

    /// <summary>Assets two active packs both change: which packs, and which one wins.</summary>
    public IReadOnlyList<string> Collisions { get; }

    public static AddonState Build(IReadOnlyList<AddonFile> files, GameData? game, IReadOnlySet<string> turnedOff, bool allOff)
    {
        var mods = game?.InstalledMods ?? [];
        var entries = files.Select(f => Judge(f, mods, turnedOff, allOff)).ToList();

        // A pack that needs other packs is active only once they are; repeat until nothing changes.
        bool changed;
        do
        {
            changed = false;
            var active = entries.Where(e => e.Status == AddonStatus.Active).Select(e => e.Pack!.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] is { Status: AddonStatus.Active, Pack: { } pack } && pack.RequiresPacks.FirstOrDefault(r => !active.Contains(r)) is { } missing)
                {
                    entries[i] = entries[i] with { Status = AddonStatus.NeedsPack, Detail = missing };
                    changed = true;
                }
            }
        }
        while (changed);

        var activePacks = entries.Where(e => e.Status == AddonStatus.Active)
            .OrderBy(e => e.Mod?.LoadOrder ?? 0)
            .ThenBy(e => e.Pack!.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Pack!)
            .ToList();

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var limits = GameLimits.Vanilla;
        if (game?.BaseSkillCap is { } baseCap && baseCap > limits.MaxSkillCap)
        {
            limits = limits with { MaxSkillCap = baseCap };
        }

        if (game?.ModSkillCap is { } modCap)
        {
            Widen("maxSkillCap", modCap.Level, modCap.Mod);
        }

        foreach (var pack in activePacks)
        {
            foreach (var (key, value) in pack.Limits)
            {
                Widen(key, value, pack.Name);
            }

            if (pack.SkillCurve.Count > 0)
            {
                Widen("maxSkillCap", pack.SkillCurve[^1].Level, pack.Name);
                Widen("maxExperience", pack.SkillCurve.Max(l => l.Experience), pack.Name);
            }
        }

        var collisions = activePacks
            .SelectMany(p => p.Assets.Distinct(StringComparer.OrdinalIgnoreCase).Select(a => (Asset: a, Pack: p.Name)))
            .GroupBy(x => x.Asset, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Pack))} ({g.Last().Pack} wins)")
            .ToList();

        return new AddonState(entries, activePacks, limits, sources, collisions);

        void Widen(string key, long value, string source)
        {
            var wider = limits.Widen(key, value);
            if (!ReferenceEquals(wider, limits))
            {
                limits = wider;
                sources[key] = source;
            }
        }
    }

    /// <summary>Every pack, active or not, that names this asset: the mods a save's content may come from.</summary>
    public IEnumerable<AddonEntry> PacksNaming(string asset) =>
        Entries.Where(e => e.Pack is { } p && p.Assets.Contains(asset, StringComparer.OrdinalIgnoreCase));

    /// <summary>Every pack, active or not, whose limits would allow <paramref name="value"/> for <paramref name="key"/>.</summary>
    public IEnumerable<AddonEntry> PacksAllowing(string key, long value) =>
        Entries.Where(e => e.Pack is { } p && Allows(p, key, value));

    private static bool Allows(AddonPack pack, string key, long value)
    {
        var limit = pack.Limits.TryGetValue(key, out var l) ? l
            : key == "maxSkillCap" && pack.SkillCurve.Count > 0 ? pack.SkillCurve[^1].Level
            : key == "maxExperience" && pack.SkillCurve.Count > 0 ? pack.SkillCurve.Max(s => s.Experience)
            : (long?)null;
        return limit is { } x && (GameLimits.IsMinimum(key) ? value >= x : value <= x);
    }

    private static AddonEntry Judge(AddonFile file, IReadOnlyList<InstalledMod> mods, IReadOnlySet<string> turnedOff, bool allOff)
    {
        if (file.Pack is not { } pack)
        {
            return new AddonEntry(file.File, null, AddonStatus.Invalid, file.Error, null);
        }

        if (!string.Equals(pack.Game, "mercenaries", StringComparison.OrdinalIgnoreCase))
        {
            return new AddonEntry(file.File, pack, AddonStatus.NotForThisGame, pack.Game, null);
        }

        var candidates = mods.Where(m => Matches(pack.Requires, m)).ToList();
        var mod = candidates.FirstOrDefault(m => m.Enabled && pack.Requires.Accepts(m.Version))
            ?? candidates.FirstOrDefault(m => m.Enabled)
            ?? candidates.FirstOrDefault();
        var status = allOff ? AddonStatus.AllTurnedOff
            : turnedOff.Contains(pack.Name) ? AddonStatus.TurnedOff
            : mod is null ? AddonStatus.ModNotInstalled
            : !mod.Enabled ? AddonStatus.ModDisabled
            : !pack.Requires.Accepts(mod.Version) ? AddonStatus.VersionNotSupported
            : AddonStatus.Active;
        var detail = status == AddonStatus.VersionNotSupported ? mod!.Version ?? "?" : null;
        return new AddonEntry(file.File, pack, status, detail, mod);
    }

    private static bool Matches(AddonRequirement requires, InstalledMod mod) =>
        (requires.ModName is { } name && string.Equals(name, mod.Name, StringComparison.OrdinalIgnoreCase))
        || (requires.ModFolder is { } folder && (string.Equals(folder, mod.Id, StringComparison.OrdinalIgnoreCase)
                                                 || string.Equals(folder, Path.GetFileName(mod.Folder), StringComparison.OrdinalIgnoreCase)));
}
