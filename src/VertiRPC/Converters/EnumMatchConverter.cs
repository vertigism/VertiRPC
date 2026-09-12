using System.Globalization;
using System.Windows.Data;

namespace VertiRPC.Converters;

/// <summary>
/// Binds a group of radio buttons to one enum property: each button passes the
/// member it stands for as the converter parameter.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && value.ToString() == parameter.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Only the button being switched on should write back; the one being
        // switched off would otherwise clobber the new value.
        if (value is not true || parameter is null)
            return Binding.DoNothing;

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Enum.TryParse(enumType, parameter.ToString(), out var parsed) ? parsed : Binding.DoNothing;
    }
}
