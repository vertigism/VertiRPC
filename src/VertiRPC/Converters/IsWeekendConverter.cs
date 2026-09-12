using System.Globalization;
using System.Windows.Data;

namespace VertiRPC.Converters;

/// <summary>
/// True for Saturdays and Sundays, so calendar day cells can colour themselves.
/// </summary>
public sealed class IsWeekendConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime date && date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
