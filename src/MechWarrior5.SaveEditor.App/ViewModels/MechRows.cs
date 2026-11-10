using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

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

    public string Subtitle => Variant.HeroName is null
        ? $"{Names.WeightClass(Variant.WeightClass)} {Variant.Tons}T"
        : $"{Loc.Text("hero", "Hero")} {Variant.ChassisName} - {Variant.Tons}T";

    public string Initials => Variant.ChassisName.Length >= 2 ? Variant.ChassisName[..2].ToUpperInvariant() : Variant.ChassisName;

    public string? DlcCode => Variant.ModName is not null ? "MOD" : Variant.Dlc?.ShortName;

    public string? DlcName => Variant.ModName is { } mod ? Loc.Text("mod_named", "Mod: {0}", mod) : Variant.Dlc?.Name;

    public bool IsMod => Variant.ModName is not null;

    public string ClassLabel => Names.WeightClass(Variant.WeightClass);

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

    public string Subtitle => Record.Variant is { } v ? $"{Names.WeightClass(v.WeightClass)} {v.Tons}T" : Loc.Text("unknown_variant", "Unknown variant");

    public string Initials => Title.Length >= 2 ? Title[..2].ToUpperInvariant() : Title;

    public string LocationLabel => Record.Location switch
    {
        MechLocation.MechBay => Loc.Instance["mechbay"],
        MechLocation.ColdStorage => Loc.Instance["cold_storage"],
        MechLocation.Unassigned => Loc.Text("unassigned", "UNASSIGNED"),
        _ => Loc.Text("market", "MARKET"),
    };

    public bool IsColdStorage => Record.Location == MechLocation.ColdStorage;

    public bool IsNew => Record.IsNew;

    public string? SarnaTitle => Record.Variant?.SarnaTitle;

    public int Tons => Record.Variant?.Tons ?? 0;
}
