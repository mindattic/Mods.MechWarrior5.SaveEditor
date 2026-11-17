namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>A skill a pilot owns: its <c>KelPilotSkillDataAsset</c> and its rank, 1 to <see cref="ClansPilot.MaxRank"/>.</summary>
public sealed record ClansPilotSkill(string Asset, int Rank);

/// <summary>
/// A pilot of the player's Star (<c>KelPilotInventoryItem</c>): unspent pilot XP and owned skills, both in its
/// <c>PilotStats</c>. The game stores a skill's rank as a 0-based <c>SkillLevelIndex</c> and omits
/// <c>PilotSkillLevels</c> until the first skill is bought.
/// </summary>
public sealed class ClansPilot
{
    public const int MaxRank = 5;

    private const string SkillAssetType = "KelPilotSkillDataAsset";

    private readonly InventoryActor _actor;
    private readonly Func<ArrayProperty?> _skillArrayTemplate;

    internal ClansPilot(InventoryActor actor, Func<ArrayProperty?> skillArrayTemplate)
    {
        _actor = actor;
        _skillArrayTemplate = skillArrayTemplate;
    }

    /// <summary>The pilot item asset, e.g. <c>Mia_PilotItem</c>.</summary>
    public string Asset => _actor.AssetName;

    public string Name => Asset.EndsWith("_PilotItem", StringComparison.Ordinal) ? Asset[..^"_PilotItem".Length] : Asset;

    private PropertyList Stats => (PropertyList)_actor.Properties.Get<StructProperty>("PilotStats").Value;

    /// <summary>Unspent pilot XP; buying a skill rank in the game spends it.</summary>
    public int Experience
    {
        get => Stats.Find<IntProperty>("PilotEXP")?.Value ?? 0;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (Stats.Find<IntProperty>("PilotEXP") is { } exp)
            {
                exp.Value = value;
            }
            else
            {
                Stats.Insert(0, new IntProperty("PilotEXP", value) { Ue5 = Ue5Tag.Simple("IntProperty") });
            }
        }
    }

    public IReadOnlyList<ClansPilotSkill> Skills =>
        [.. SkillElements().Select(e => new ClansPilotSkill(SkillAsset(e), e.Find<IntProperty>("SkillLevelIndex")?.Value + 1 ?? 1))];

    public bool CanAddSkills => SkillArray() is not null || _skillArrayTemplate() is not null;

    /// <summary>Sets an owned skill's rank, or adds the skill at that rank.</summary>
    public void SetSkill(string asset, int rank)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asset);
        ArgumentOutOfRangeException.ThrowIfLessThan(rank, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rank, MaxRank);
        var element = SkillElements().FirstOrDefault(e => SkillAsset(e) == asset) ?? AddSkillElement(asset);
        element.Get<IntProperty>("SkillLevelIndex").Value = rank - 1;
    }

    public void RemoveSkill(string asset)
    {
        if (SkillArray() is not { Storage: StructArrayStorage storage })
        {
            return;
        }

        storage.Elements.RemoveAll(e => e is PropertyList list && SkillAsset(list) == asset);
        if (storage.Elements.Count == 0)
        {
            Stats.Remove("PilotSkillLevels");
        }
    }

    internal ArrayProperty? SkillArray() => Stats.Find<ArrayProperty>("PilotSkillLevels");

    private IEnumerable<PropertyList> SkillElements() =>
        SkillArray()?.Storage is StructArrayStorage storage ? storage.PropertyElements : [];

    private static string SkillAsset(PropertyList element) =>
        (element.Find<StructProperty>("SkillAssetId")?.Value as PropertyList)?.Find<NameProperty>("PrimaryAssetName")?.Value.Value ?? string.Empty;

    private PropertyList AddSkillElement(string asset)
    {
        var array = SkillArray();
        var fresh = array is null;
        if (array is null)
        {
            // Modelled on another pilot's skill list, so the new array carries the game's own array and struct tags.
            array = (ArrayProperty)(_skillArrayTemplate() ?? throw new InvalidOperationException("No pilot in this save has a skill to model a new one on.")).DeepClone();
            Stats.Insert(Stats.IndexOf("PilotEXP") + 1, array);
        }

        var storage = array.Structs;
        var element = storage.PropertyElements.First().DeepClone();
        if (fresh)
        {
            storage.Elements.Clear();
        }

        var id = (PropertyList)element.Get<StructProperty>("SkillAssetId").Value;
        id.Get<NameProperty>("PrimaryAssetName").Value = asset;
        if (id.Find<StructProperty>("PrimaryAssetType")?.Value is PropertyList type)
        {
            type.Get<NameProperty>("Name").Value = SkillAssetType;
        }

        storage.Elements.Add(element);
        return element;
    }
}
