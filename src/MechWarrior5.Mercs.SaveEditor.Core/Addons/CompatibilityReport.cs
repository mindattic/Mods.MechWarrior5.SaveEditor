using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.Mercs.SaveEditor.Core.Addons;

public enum CompatibilitySeverity
{
    /// <summary>Differs from vanilla because of a mod or pack that is active now: expected.</summary>
    Modded,

    /// <summary>Needs a mod that is missing, disabled, the wrong version, or unknown: the game may not load it.</summary>
    Problem,
}

/// <summary>
/// One way a save differs from the vanilla game: what (<see cref="Subject"/>), its value, and the mod behind it with its
/// version and page, when one is known. <see cref="Reason"/> says why that mod doesn't cover it today.
/// </summary>
public sealed record CompatibilityLine(
    CompatibilitySeverity Severity,
    string Category,
    string Subject,
    string Value,
    string? Mod,
    string? Version,
    string? Url,
    string Reason);

/// <summary>
/// Read-only: lists every way an open save deviates from vanilla (content the base game doesn't have, values past the
/// base game's limits) and traces each one to the mod that explains it, from the installed mods (enabled or not) and
/// every add-on pack (active or not). Nothing here changes the save.
/// </summary>
public static class CompatibilityReport
{
    public static IReadOnlyList<CompatibilityLine> Build(Mw5Document doc, GameData? game, AddonState addons)
    {
        var lines = new List<CompatibilityLine>();
        if (game is not null)
        {
            foreach (var reference in SaveValidator.ReferencedAssets(doc.File))
            {
                if (!game.BaseAssetNames.Contains(reference.AssetName))
                {
                    lines.Add(ForAsset(reference, game, addons));
                }
            }
        }

        var vanilla = GameLimits.Vanilla;
        foreach (var pilot in doc.Roster.Pilots)
        {
            foreach (var skill in pilot.Skills)
            {
                if (skill.HasCap)
                {
                    Value("Skill cap", $"{pilot.DisplayName}: {skill.Name}", "maxSkillCap", skill.Cap);
                }

                Value("Experience", $"{pilot.DisplayName}: {skill.Name}", "maxExperience", skill.Experience);
            }

            Value("Salary", pilot.DisplayName, "maxSalary", pilot.Salary);
        }

        Value("C-Bills", "Company", "maxCBills", doc.Company.CBills);
        foreach (var standing in doc.Factions.Standings)
        {
            Value("Standing", standing.Faction, standing.Standing < 0 ? "minStanding" : "maxStanding", standing.Standing);
        }

        foreach (var item in doc.Inventory.Items)
        {
            Value("Stack size", item.Asset, "maxCount", item.Count);
        }

        return [.. lines.OrderByDescending(l => l.Severity).ThenBy(l => l.Mod ?? "￿", StringComparer.OrdinalIgnoreCase).ThenBy(l => l.Category).ThenBy(l => l.Subject)];

        void Value(string category, string subject, string key, long value)
        {
            var limit = vanilla[key];
            if (GameLimits.IsMinimum(key) ? value >= limit : value <= limit)
            {
                return;
            }

            var shown = value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            var bound = $"vanilla {(GameLimits.IsMinimum(key) ? "minimum" : "maximum")} {limit:N0}";
            var active = addons.Limits[key];
            if (GameLimits.IsMinimum(key) ? value >= active : value <= active)
            {
                var source = addons.LimitSources.GetValueOrDefault(key);
                var mod = source is null ? null : game?.InstalledMods.FirstOrDefault(m => m.Name == source);
                lines.Add(new CompatibilityLine(CompatibilitySeverity.Modded, category, subject, shown, source, mod?.Version, mod?.Url ?? PackUrl(addons, source), $"past the {bound}, allowed by this mod"));
                return;
            }

            if (addons.PacksAllowing(key, value).FirstOrDefault() is { } pack)
            {
                lines.Add(FromPack(pack, category, subject, shown, $"past the {bound}; this mod allows it but {Why(pack)}"));
                return;
            }

            lines.Add(new CompatibilityLine(CompatibilitySeverity.Problem, category, subject, shown, null, null, null,
                $"past the {bound}; no installed mod or known add-on allows it"));
        }
    }

    private static CompatibilityLine ForAsset(MissingAsset reference, GameData game, AddonState addons)
    {
        var category = reference.AssetType.Replace("MW", string.Empty, StringComparison.Ordinal).Replace("DataAsset", string.Empty, StringComparison.Ordinal).Replace("Asset", string.Empty, StringComparison.Ordinal);
        var value = reference.References == 1 ? "1 use" : $"{reference.References} uses";
        if (game.ModAssets.TryGetValue(reference.AssetName, out var mod))
        {
            // A pack's page link (the mod's own page) beats mod.json's author link.
            var url = addons.PacksNaming(reference.AssetName).Select(p => p.Pack!.Source).FirstOrDefault(s => s is not null) ?? mod.Url;
            return mod.Enabled && game.AssetNames.Contains(reference.AssetName)
                ? new CompatibilityLine(CompatibilitySeverity.Modded, category, reference.AssetName, value, mod.Name, mod.Version, url, "from this enabled mod")
                : new CompatibilityLine(CompatibilitySeverity.Problem, category, reference.AssetName, value, mod.Name, mod.Version, url,
                    "this mod is installed but disabled: enable it in the game's mod list");
        }

        if (addons.PacksNaming(reference.AssetName).FirstOrDefault() is { } pack)
        {
            return FromPack(pack, category, reference.AssetName, value, Why(pack));
        }

        return new CompatibilityLine(CompatibilitySeverity.Problem, category, reference.AssetName, value, null, null, null,
            "not in the game or any installed mod: from a mod that isn't installed");
    }

    private static CompatibilityLine FromPack(AddonEntry pack, string category, string subject, string value, string reason) =>
        new(pack.Status == AddonStatus.Active ? CompatibilitySeverity.Modded : CompatibilitySeverity.Problem,
            category, subject, value,
            pack.Pack!.Requires.Label,
            pack.Mod?.Version ?? (pack.Pack.Requires.Versions.Count > 0 ? string.Join(" / ", pack.Pack.Requires.Versions) : null),
            pack.Pack.Source ?? pack.Mod?.Url,
            reason);

    private static string Why(AddonEntry pack) => pack.Status switch
    {
        AddonStatus.Active => "from this mod",
        AddonStatus.ModNotInstalled => "this mod isn't installed",
        AddonStatus.ModDisabled => "this mod is installed but disabled: enable it in the game's mod list",
        AddonStatus.VersionNotSupported => $"the installed version ({pack.Detail}) isn't one its add-on supports ({string.Join(", ", pack.Pack!.Requires.Versions)})",
        AddonStatus.NeedsPack => $"its add-on also needs \"{pack.Detail}\"",
        AddonStatus.TurnedOff or AddonStatus.AllTurnedOff => "its add-on is turned off in Settings",
        _ => "its add-on can't be used",
    };

    private static string? PackUrl(AddonState addons, string? name) =>
        name is null ? null : addons.Active.FirstOrDefault(p => p.Name == name)?.Source;
}
