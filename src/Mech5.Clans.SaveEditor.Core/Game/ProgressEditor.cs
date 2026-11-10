namespace Mech5.Clans.SaveEditor.Core.Game;

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

    /// <summary>Total Honor experience on the Honor leveling track; null when the track is absent.</summary>
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
