using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

namespace MechWarrior5.SaveEditor.App.ViewModels;

public sealed partial class StartViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private CampaignInfo? _selectedCampaign;

    public StartViewModel(MainViewModel main)
    {
        _main = main;
        Refresh();
    }

    public ObservableCollection<CampaignInfo> Campaigns { get; } = [];

    public bool HasCampaigns => Campaigns.Count > 0;

    public string SaveRoot { get; } = CampaignLibrary.DefaultSaveRoot;

    public void Refresh()
    {
        Campaigns.Clear();
        foreach (var campaign in CampaignLibrary.Scan())
        {
            Campaigns.Add(campaign);
        }

        var last = _main.Settings.LastSavePath;
        SelectedCampaign = Campaigns.FirstOrDefault(c =>
                last is not null && string.Equals(c.Folder, Path.GetDirectoryName(last), StringComparison.OrdinalIgnoreCase))
            ?? Campaigns.FirstOrDefault();
        OnPropertyChanged(nameof(HasCampaigns));
    }

    [RelayCommand]
    private void Open(SaveInfo? save)
    {
        if (save is not null)
        {
            _main.OpenPath(save.Path);
        }
    }

    [RelayCommand]
    private void Rescan() => Refresh();
}
