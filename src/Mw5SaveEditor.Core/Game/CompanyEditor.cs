using Mw5SaveEditor.Core.Saves;
using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Game;

public sealed class CompanyEditor
{
    public const long MaxCBills = 2_000_000_000;

    private readonly PropertyList _inventory;
    private readonly PropertyList _company;
    private readonly TextProperty? _name;
    private readonly Action _changed;

    internal CompanyEditor(Mw5SaveFile file, Action changed)
    {
        _changed = changed;
        _inventory = file.GetModel("InventoryModel");
        _company = file.GetModel("MercCompanyModel");
        _name = _company.Find<TextProperty>("CompanyName");
        CampaignDate = ReadCampaignDate(file.TryGetModel("TimelineModel"));
    }

    public long CBills
    {
        get => (_inventory.Find<StructProperty>("AvailableCBills")?.Value as PropertyList)
            ?.Find<Int64Property>("Value")?.Value ?? 0;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxCBills);
            if (CBills == clamped)
            {
                return;
            }

            if (_inventory.Find<StructProperty>("AvailableCBills")?.Value is not PropertyList bigInt)
            {
                bigInt = [];
                _inventory.Add(new StructProperty("AvailableCBills", "MWBigInt", bigInt));
            }

            if (bigInt.Find<Int64Property>("Value") is { } stored)
            {
                stored.Value = clamped;
            }
            else
            {
                bigInt.Add(new Int64Property("Value", clamped));
            }

            _changed();
        }
    }

    /// <summary>Company reputation points; created on first edit since Unreal omits a zero value.</summary>
    public int Reputation
    {
        get => _company.Find<IntProperty>("Reputation")?.Value ?? 0;
        set
        {
            var clamped = Math.Max(0, value);
            if (Reputation == clamped)
            {
                return;
            }

            var property = _company.Find<IntProperty>("Reputation");
            if (property is null)
            {
                _company.Insert(0, new IntProperty("Reputation", clamped));
            }
            else
            {
                property.Value = clamped;
            }

            _changed();
        }
    }

    public string CompanyName
    {
        get => _name?.DisplayString ?? string.Empty;
        set
        {
            if (_name is null || value == CompanyName || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _name.Data = FText.WithDisplayString(_name.Data, value.Trim());
            _changed();
        }
    }

    /// <summary>In-game date: campaign start plus days elapsed. Read-only (the timeline drives events).</summary>
    public DateTime? CampaignDate { get; }

    private static DateTime? ReadCampaignDate(PropertyList? timeline)
    {
        var start = timeline?.Find<Int64Property>("StartingDateTicks")?.Value;
        var days = timeline?.Find<IntProperty>("TotalTimeElapsed")?.Value ?? 0;
        return start is > 0 and < 3_155_378_975_999_999_999 ? new DateTime(start.Value).AddDays(days) : null;
    }
}
