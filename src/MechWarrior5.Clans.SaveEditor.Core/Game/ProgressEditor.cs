namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>A research job in the queue or completed.</summary>
public sealed record ResearchEntry(string Job, int Level, float PointsApplied);

/// <summary>
/// Campaign progress kept in <c>PersistentModelData</c> that mirrors inventory values: Honor's
/// leveling track follows the Honor currency, and the technician pool follows the technician crew.
/// </summary>
public sealed class ProgressEditor
{
    private readonly PropertyList _models;

    internal ProgressEditor(PropertyList models)
    {
        _models = models;
    }

    /// <summary>
    /// Honor experience needed for each level from 2 up, cumulative, as the game's HonorLevelingAsset defines it
    /// (matches every TotalEXP / CurrentDisplayLevel pair in real saves).
    /// </summary>
    public static readonly IReadOnlyList<int> HonorLevelThresholds = [100, 225, 370, 535, 720, 930, 1180, 1470, 1800, 2170, 2845, 3745, 5295, 7270];

    /// <summary>The Honor level a given total experience reaches (1 to 15).</summary>
    public static int HonorLevelFor(int experience) => 1 + HonorLevelThresholds.Count(t => experience >= t);

    /// <summary>
    /// Total Honor experience on the Honor leveling track; null when the track is absent. Setting it moves the level
    /// with it; lowering it also lowers the awarded level and last-seen experience, which may not exceed the current
    /// ones (raising leaves them, so the game awards the new levels itself).
    /// </summary>
    public int? HonorExperience
    {
        get => HonorTrack()?.Find<IntProperty>("TotalEXP")?.Value;
        set
        {
            if (value is not { } exp || HonorTrack() is not { } track)
            {
                return;
            }

            SetInt(track, "TotalEXP", exp);
            var level = HonorLevelFor(exp);
            SetInt(track, "CurrentDisplayLevel", level);
            if (track.Find<IntProperty>("LastAwardedDisplayLevel") is { } awarded && awarded.Value > level)
            {
                awarded.Value = level;
            }

            if (track.Find<IntProperty>("LastSeenExp") is { } seen && seen.Value > exp)
            {
                seen.Value = exp;
            }
        }
    }

    public int? HonorLevel => HonorTrack()?.Find<IntProperty>("CurrentDisplayLevel")?.Value;

    /// <summary>The technician pool the mech bay draws on.</summary>
    public int? TechniciansAvailable
    {
        get => Model("TechnicianAssignmentModel")?.Find<IntProperty>("TotalAvailableTechs")?.Value;
        set
        {
            if (value is { } techs && Model("TechnicianAssignmentModel") is { } model)
            {
                SetInt(model, "TotalAvailableTechs", techs);
            }
        }
    }

    public int? CyclesElapsed => Model("CycleTimeModel")?.Find<IntProperty>("TotalCyclesElapsed")?.Value;

    public IReadOnlyList<ResearchEntry> ResearchQueue => Research("ResearchJobQueue");

    public IReadOnlyList<ResearchEntry> CompletedResearch => Research("CompletedResearchJobs");

    private PropertyList? Model(string name) => _models.Find<ObjectProperty>(name)?.Inline;

    private PropertyList? HonorTrack() =>
        Model("MetaLevelingModel")?.Find<ArrayProperty>("LevelProgressions")?.Storage is StructArrayStorage tracks
            ? tracks.PropertyElements
                .FirstOrDefault(t => Ue.AssetName(t.Find<StructProperty>("LevelingAssetId")?.Value as PropertyList) == "HonorLevelingAsset")
                ?.Find<StructProperty>("Data")?.Value as PropertyList
            : null;

    private IReadOnlyList<ResearchEntry> Research(string list) =>
        Model("ResearchJobModel")?.Find<ArrayProperty>(list)?.Storage is StructArrayStorage jobs
            ? [.. jobs.PropertyElements.Select(job =>
            {
                var key = job.Find<StructProperty>("ResearchJobKey")?.Value as PropertyList;
                var asset = key?.Find<StructProperty>("ResearchJobAssetId")?.Value as PropertyList;
                return new ResearchEntry(
                    asset?.Find<NameProperty>("PrimaryAssetName")?.Value.Value ?? "?",
                    key?.Find<IntProperty>("LevelIndex")?.Value ?? 0,
                    job.Find<FloatProperty>("ResearchPointsApplied")?.Value ?? 0);
            })]
            : [];

    private static void SetInt(PropertyList list, string name, int value)
    {
        if (list.Find<IntProperty>(name) is { } existing)
        {
            existing.Value = value;
        }
        else
        {
            list.Add(new IntProperty(name, value) { Ue5 = Ue5Tag.Simple("IntProperty") });
        }
    }
}
