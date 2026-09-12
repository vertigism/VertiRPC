using System.Windows;

namespace VertiRPC.Themes;

/// <summary>
/// Swaps the palette dictionary the control styles read through
/// <c>DynamicResource</c>, which restyles the open window in place.
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// App.xaml keeps the palette first in its merged dictionaries; the control
    /// styles that follow depend on it being replaced rather than appended.
    /// </summary>
    private const int PaletteIndex = 0;

    public static void Apply(bool pink)
    {
        var palette = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/VertiRPC;component/Themes/{(pink ? "Pink" : "Dark")}.xaml"),
        };

        Application.Current.Resources.MergedDictionaries[PaletteIndex] = palette;
    }
}
