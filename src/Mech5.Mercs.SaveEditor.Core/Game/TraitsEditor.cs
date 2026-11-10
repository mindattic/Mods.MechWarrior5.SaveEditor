using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Saves;

namespace Mech5.Mercs.SaveEditor.Core.Game;

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
}

/// <summary>Arrays of typed trait ids (<c>{ ID: PrimaryAssetId }</c>), shared by pilots, mechs and unlocks.</summary>
internal static class TraitIds
{
    public static List<string> Read(StructArrayStorage? array) =>
        array?.PropertyElements.Select(e => Ue.AssetName(e)).OfType<string>().ToList() ?? [];

    public static void Add(PropertyList owner, string arrayName, string structType, string asset, string? insertAfter = null)
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

        array.Elements.Add(new PropertyList([Ue.PrimaryAssetId(PilotCatalog.MechTraitType, asset)]));
    }

    public static bool Remove(StructArrayStorage? array, string asset)
    {
        var element = array?.PropertyElements.FirstOrDefault(e =>
            string.Equals(Ue.AssetName(e), asset, StringComparison.OrdinalIgnoreCase));
        return element is not null && array!.Elements.Remove(element);
    }
}
