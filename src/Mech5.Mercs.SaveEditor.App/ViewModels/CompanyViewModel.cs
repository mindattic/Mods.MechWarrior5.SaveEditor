using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

public sealed record DlcRow(string Code, string Name, bool Owned);

public sealed partial class CompanyViewModel : ObservableObject
{
    private readonly Mw5Document _doc;

    public CompanyViewModel(Mw5Document doc, MechCatalog catalog)
    {
        _doc = doc;
        Dlcs = catalog.DlcPacks.Select(p => new DlcRow(p.ShortName, p.Name, doc.Dlc.Tags.Contains(p.Tag))).ToList();
        DlcNote = doc.Dlc.Source switch
        {
            DlcSource.ThisSave => "Read from this save's campaign index.",
            DlcSource.CampaignLatestSave => "This save isn't in its campaign index; showing the campaign's latest save.",
            _ => "No Campaign.json beside this save, so DLC ownership can't be checked.",
        };
    }

    public IReadOnlyList<DlcRow> Dlcs { get; }

    public string DlcNote { get; }

    public long CBills
    {
        get => _doc.Company.CBills;
        set
        {
            _doc.Company.CBills = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CBillsText));
        }
    }

    public string CBillsText => CBills.ToString("N0", CultureInfo.CurrentCulture);

    public int Reputation
    {
        get => _doc.Company.Reputation;
        set
        {
            _doc.Company.Reputation = value;
            OnPropertyChanged();
        }
    }

    public string CompanyName
    {
        get => _doc.Company.CompanyName;
        set
        {
            _doc.Company.CompanyName = value;
            OnPropertyChanged();
        }
    }

    public string CampaignDate => _doc.Company.CampaignDate?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) ?? "Unknown";

    public string? GameVersion => _doc.File.ProjectVersion;

    public int MechBayCount => _doc.Hangar.Mechs.Count(m => m.Location == MechLocation.MechBay);

    public int ColdStorageCount => _doc.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage);

    public int PilotCount => _doc.Roster.Pilots.Count;

    public int ItemCount => _doc.Inventory.Items.Sum(i => i.Count);

    public void Refresh() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private void AddCBills(string amount) =>
        CBills += long.Parse(amount, CultureInfo.InvariantCulture);

    [RelayCommand]
    private void MaxCBills() => CBills = CompanyEditor.MaxCBills;
}
