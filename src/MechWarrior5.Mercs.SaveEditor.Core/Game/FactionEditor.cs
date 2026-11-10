using MechWarrior5.Mercs.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

public sealed class FactionEditor
{
    public const int MinStanding = -100;
    public const int MaxStanding = 100;

    internal FactionEditor(Mw5SaveFile file, Action changed)
    {
        var list = file.GetModel("MercCompanyModel")
            .Find<StructProperty>("FactionStandingList")?.Value as PropertyList;
        var standings = list?.Find<ArrayProperty>("Standings")?.Storage as StructArrayStorage;
        Standings = standings?.PropertyElements
            .Select(e => new FactionStanding(e, changed))
            .OrderBy(f => f.Faction, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    public IReadOnlyList<FactionStanding> Standings { get; }

    public void SetAll(int standing)
    {
        foreach (var faction in Standings)
        {
            faction.Standing = standing;
        }
    }
}

public sealed class FactionStanding
{
    private readonly PropertyList _element;
    private readonly Action _changed;

    internal FactionStanding(PropertyList element, Action changed)
    {
        _element = element;
        _changed = changed;
        Faction = Ue.AssetName(element.Find<StructProperty>("Faction")?.Value as PropertyList) ?? "Unknown";
    }

    public string Faction { get; }

    /// <summary>Unreal omits a zero standing from the save, so the property is created on first edit.</summary>
    public int Standing
    {
        get => _element.Find<IntProperty>("Standing")?.Value ?? 0;
        set
        {
            var clamped = Math.Clamp(value, FactionEditor.MinStanding, FactionEditor.MaxStanding);
            if (Standing == clamped)
            {
                return;
            }

            var property = _element.Find<IntProperty>("Standing");
            if (property is null)
            {
                _element.Add(new IntProperty("Standing", clamped));
            }
            else
            {
                property.Value = clamped;
            }

            _changed();
        }
    }
}
