using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

/// <summary>
/// Moves the campaign calendar. The current date is the start date plus the days elapsed, and the save
/// also holds absolute dates everywhere (payroll and pilot-recovery events, contract expiries, logs), so
/// the start date and every <c>DateTime</c> in every model shift by the same amount: payroll stays due in
/// the same number of days, injuries heal on schedule. Dates inside opaque data (raw maps) don't move.
/// </summary>
public static class CampaignCalendar
{
    public static DateTime? Current(Mw5SaveFile file)
    {
        var timeline = file.TryGetModel("TimelineModel");
        var start = timeline?.Find<Int64Property>("StartingDateTicks")?.Value;
        var days = timeline?.Find<IntProperty>("TotalTimeElapsed")?.Value ?? 0;
        return start is > 0 and < 3_155_378_975_999_999_999 ? new DateTime(start.Value).AddDays(days) : null;
    }

    /// <summary>Shifts the calendar so the current date becomes <paramref name="date"/> (time of day kept); returns the days moved.</summary>
    public static int MoveTo(Mw5SaveFile file, DateTime date)
    {
        if (Current(file) is not { } now || file.TryGetModel("TimelineModel")?.Find<Int64Property>("StartingDateTicks") is not { } start)
        {
            throw new InvalidOperationException("This save has no campaign calendar.");
        }

        var days = (int)Math.Round((date.Date - now.Date).TotalDays);
        if (days == 0)
        {
            return 0;
        }

        var delta = TimeSpan.FromDays(days).Ticks;
        // Keep the start date (and so every event after it) a real date: a start at or before year 1 can't be read back.
        if (start.Value + delta < TimeSpan.FromDays(366).Ticks || date.Year > 9000)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "That date is too far back or ahead for the campaign calendar.");
        }

        start.Value += delta;
        foreach (var model in file.Models.OfType<ObjectProperty>())
        {
            if (model.Inline is { } list)
            {
                Shift(list, delta);
            }
        }

        return days;
    }

    private static void Shift(PropertyList list, long delta)
    {
        foreach (var property in list)
        {
            switch (property)
            {
                case StructProperty { StructType.Value: "DateTime", Value: RawStructValue raw }:
                    ShiftTicks(raw, delta);
                    break;
                case StructProperty { Value: PropertyList nested }:
                    Shift(nested, delta);
                    break;
                case ArrayProperty { Storage: StructArrayStorage array }:
                    foreach (var element in array.Elements)
                    {
                        if (array.StructType.Value == "DateTime" && element is RawStructValue rawElement)
                        {
                            ShiftTicks(rawElement, delta);
                        }
                        else if (element is PropertyList nestedElement)
                        {
                            Shift(nestedElement, delta);
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>Unset dates (0 ticks) stay unset.</summary>
    private static void ShiftTicks(RawStructValue raw, long delta)
    {
        if (raw.Data.Length != 8)
        {
            return;
        }

        var ticks = BitConverter.ToInt64(raw.Data);
        if (ticks <= 0)
        {
            return;
        }

        var shifted = Math.Clamp(ticks + delta, 1, DateTime.MaxValue.Ticks);
        raw.Data = BitConverter.GetBytes(shifted);
    }
}
