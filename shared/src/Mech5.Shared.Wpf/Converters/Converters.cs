using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Mech5.Shared.Wpf.Converters;

/// <summary>True → Visible. Pass <c>Invert</c> as the parameter to flip.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = value is true;
        if (parameter is "Invert")
        {
            on = !on;
        }

        return on ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Non-null / non-empty / non-zero → Visible. <c>Invert</c> flips.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value switch
        {
            null => false,
            string s => s.Length > 0,
            int i => i != 0,
            _ => true,
        };
        if (parameter is "Invert")
        {
            has = !has;
        }

        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Checks a value against the enum member named by the parameter (for nav radio buttons).</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Maps a faction standing (-100..100) to a bar width within one half of the meter.</summary>
public sealed class StandingToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int standing, double halfWidth] || halfWidth <= 0)
        {
            return 0.0;
        }

        var amount = parameter is "Positive" ? Math.Max(0, standing) : Math.Max(0, -standing);
        return halfWidth * Math.Min(amount, 100) / 100.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
