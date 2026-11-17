using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

/// <summary>
/// Company-wide 'Mech trait unlocks (<c>MechTraitModel.UnlockedMechTraitsList</c>) and the Cantina lock
/// (<c>ErrandModel.bAreCantinasLocked</c>). Traits installed on one 'Mech live in its loadout; see
/// <see cref="HangarEditor.MechTraits"/>.
/// </summary>
public sealed class TraitsEditor
{
    private readonly PropertyList? _traitModel;
    private readonly PropertyList? _errands;
    private readonly Action _changed;

    internal TraitsEditor(Mw5SaveFile file, Action changed)
    {
        _traitModel = file.TryGetModel("MechTraitModel");
        _errands = file.TryGetModel("ErrandModel");
        _changed = changed;
    }

    public bool HasMechTraits => _traitModel is not null;

    public IReadOnlyList<string> UnlockedMechTraits => TraitIds.Read(Unlocked);

    public bool HasCantinas => _errands is not null;

    /// <summary>Unreal omits the flag when false, so a missing flag means the Cantinas are open.</summary>
    public bool CantinasLocked
    {
        get => _errands?.Find<BoolProperty>("bAreCantinasLocked")?.Value ?? false;
        set
        {
            if (_errands is null || CantinasLocked == value)
            {
                return;
            }

            if (_errands.Find<BoolProperty>("bAreCantinasLocked") is { } flag)
            {
                flag.Value = value;
            }
            else
            {
                _errands.Insert(0, new BoolProperty("bAreCantinasLocked", value));
            }

            _changed();
        }
    }

    public bool UnlockMechTrait(string asset)
    {
        if (_traitModel is null || UnlockedMechTraits.Contains(asset, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        TraitIds.Add(_traitModel, "UnlockedMechTraitsList", "MechTraitDataAssetId", asset);
        _changed();
        return true;
    }

    public bool LockMechTrait(string asset)
    {
        if (!TraitIds.Remove(Unlocked, asset))
        {
            return false;
        }

        _changed();
        return true;
    }

    private StructArrayStorage? Unlocked =>
        _traitModel?.Find<ArrayProperty>("UnlockedMechTraitsList")?.Storage as StructArrayStorage;

    /// <summary>Cantina career points per path (<c>ErrandModel.CareerPathList.Progress</c>), by career path asset.</summary>
    public IReadOnlyDictionary<string, int> CareerPoints =>
        (CareerProgress?.PropertyElements ?? [])
            .Select(e => (Asset: Ue.AssetName(e.Find<StructProperty>("Career")?.Value as PropertyList), Points: e.Find<IntProperty>("Progress")?.Value ?? 0))
            .Where(c => c.Asset is not null)
            .GroupBy(c => c.Asset!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Points, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when the save has its career list, so a path not started yet can be added in the game's own element layout.
    /// (Before the first Cantina job the game hasn't written the list, and the editor doesn't invent it.)
    /// </summary>
    public bool CanStartCareers => CareerProgress is not null;

    /// <summary>
    /// Sets a career path's points (never below 0). Rewards are left to the game, which offers them to claim in the
    /// Cantina once a level is reached. False when nothing changed or the path can't be added to this save.
    /// </summary>
    public bool SetCareerPoints(string careerAsset, int points)
    {
        points = Math.Max(0, points);
        if (CareerProgress is not { } list)
        {
            return false;
        }

        var element = list.PropertyElements.FirstOrDefault(e =>
            string.Equals(Ue.AssetName(e.Find<StructProperty>("Career")?.Value as PropertyList), careerAsset, StringComparison.OrdinalIgnoreCase));
        if (element is null)
        {
            if (points == 0)
            {
                return false;
            }

            list.Elements.Add(new PropertyList(
            [
                Ue.AssetId("Career", "CareerPathAssetId", CareerPathAssetType, careerAsset),
                new IntProperty("Progress", points),
            ]));
        }
        else if (element.Find<IntProperty>("Progress") is { } progress)
        {
            if (progress.Value == points)
            {
                return false;
            }

            progress.Value = points;
        }
        else
        {
            element.Add(new IntProperty("Progress", points));
        }

        _changed();
        return true;
    }

    private const string CareerPathAssetType = "MWCareerPathAsset";

    private StructArrayStorage? CareerProgress =>
        ((_errands?.Find<StructProperty>("CareerPathList")?.Value as PropertyList)?.Find<ArrayProperty>("Progress"))?.Storage as StructArrayStorage;
}

/// <summary>Arrays of typed trait ids (<c>{ ID: PrimaryAssetId }</c>), shared by pilots, mechs and unlocks.</summary>
internal static class TraitIds
{
    public static List<string> Read(StructArrayStorage? array) =>
        array?.PropertyElements.Select(e => Ue.AssetName(e)).OfType<string>().ToList() ?? [];

    public static void Add(PropertyList owner, string arrayName, string structType, string asset, string? insertAfter = null,
        string assetType = PilotCatalog.MechTraitType)
    {
        if (owner.Find<ArrayProperty>(arrayName)?.Storage is not StructArrayStorage array)
        {
            var created = Ue.StructArray(arrayName, structType, []);
            var at = insertAfter is null ? -1 : owner.IndexOf(insertAfter);
            if (at >= 0)
            {
                owner.Insert(at + 1, created);
            }
            else
            {
                owner.Add(created);
            }

            array = created.Structs;
        }

        array.Elements.Add(new PropertyList([Ue.PrimaryAssetId(assetType, asset)]));
    }

    public static bool Remove(StructArrayStorage? array, string asset)
    {
        var element = array?.PropertyElements.FirstOrDefault(e =>
            string.Equals(Ue.AssetName(e), asset, StringComparison.OrdinalIgnoreCase));
        return element is not null && array!.Elements.Remove(element);
    }
}
