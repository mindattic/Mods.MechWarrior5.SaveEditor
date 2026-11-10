using Mech5.Mercs.SaveEditor.App.Services;
using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

/// <summary>A purchasable variant in the requisition list.</summary>
public sealed partial class CatalogMechViewModel : SelectableItem
{
    public CatalogMechViewModel(MechVariant variant, bool dlcOwned)
    {
        Variant = variant;
        IsDlcOwned = dlcOwned;
        SearchText = $"{variant.Code} {variant.ChassisName} {variant.Chassis} {variant.HeroName} {variant.Dlc?.Name}".ToUpperInvariant();
    }

    public MechVariant Variant { get; }

    public string Code => Variant.Code;

    public string Title => Variant.HeroName ?? Variant.ChassisName;

    public string Subtitle => Variant.HeroName is null ? $"{Variant.WeightClass} {Variant.Tons}T" : $"Hero {Variant.ChassisName} - {Variant.Tons}T";

    public string Initials => Variant.ChassisName.Length >= 2 ? Variant.ChassisName[..2].ToUpperInvariant() : Variant.ChassisName;

    public string? DlcCode => Variant.Dlc?.ShortName;

    public string? DlcName => Variant.Dlc?.Name;

    public bool IsHero => Variant.HeroName is not null;

    public bool IsDlcOwned { get; }

    public string SarnaTitle => Variant.SarnaTitle;

    public Uri SarnaUrl => MechImageService.PageUrl(Variant.SarnaTitle);

    public string SearchText { get; }

    public int OwnedCount { get; private set; }

    public void SetOwnedCount(int count)
    {
        OwnedCount = count;
        OnPropertyChanged(nameof(OwnedCount));
    }
}

/// <summary>A mech the company holds (hangar bay or cold storage).</summary>
public sealed partial class HangarMechViewModel : SelectableItem
{
    public HangarMechViewModel(MechRecord record)
    {
        Record = record;
    }

    public MechRecord Record { get; }

    public string Code => Record.Code;

    public string Title => Record.CustomName is { Length: > 0 } custom
        ? custom
        : Record.Variant?.HeroName ?? Record.Variant?.ChassisName ?? Record.Asset;

    public string Subtitle => Record.Variant is { } v ? $"{v.WeightClass} {v.Tons}T" : "Unknown variant";

    public string Initials => Title.Length >= 2 ? Title[..2].ToUpperInvariant() : Title;

    public string LocationLabel => Record.Location switch
    {
        MechLocation.MechBay => "MECHBAY",
        MechLocation.ColdStorage => "COLD STORAGE",
        MechLocation.Unassigned => "UNASSIGNED",
        _ => "MARKET",
    };

    public bool IsColdStorage => Record.Location == MechLocation.ColdStorage;

    public bool IsNew => Record.IsNew;

    public string? SarnaTitle => Record.Variant?.SarnaTitle;

    public int Tons => Record.Variant?.Tons ?? 0;
}
