using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VertiRPC.Controls;

/// <summary>
/// A single date-and-time field with a calendar popup, which WPF has no
/// equivalent of. The text is editable directly, in the same
/// <c>dd.MM.yyyy HH:mm:ss</c> shape the Qt build used.
/// </summary>
public partial class DateTimePicker : UserControl
{
    private const string DisplayFormat = "dd.MM.yyyy HH:mm:ss";

    /// <summary>Typing the seconds is optional, and either separator style is taken.</summary>
    private static readonly string[] AcceptedFormats =
    [
        "dd.MM.yyyy HH:mm:ss",
        "dd.MM.yyyy HH:mm",
        "d.M.yyyy HH:mm:ss",
        "d.M.yyyy HH:mm",
        "dd/MM/yyyy HH:mm:ss",
        "dd-MM-yyyy HH:mm:ss",
    ];

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(DateTime),
        typeof(DateTimePicker),
        new FrameworkPropertyMetadata(
            DateTime.Now,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    /// <summary>Guards against the control's own edits being read back as user input.</summary>
    private bool _updatingText;

    public DateTimePicker()
    {
        InitializeComponent();
        ShowValue(Value);
    }

    public DateTime Value
    {
        get => (DateTime)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((DateTimePicker)sender).ShowValue((DateTime)args.NewValue);

    private void ShowValue(DateTime value)
    {
        _updatingText = true;
        TextPart.Text = value.ToString(DisplayFormat, CultureInfo.InvariantCulture);
        _updatingText = false;
    }

    private void OnPopupOpened(object? sender, EventArgs args)
    {
        _updatingText = true;
        // Always open on the day grid: the mode otherwise persists from the
        // last time the header was clicked, leaving a year or decade view.
        CalendarPart.DisplayMode = CalendarMode.Month;
        CalendarPart.SelectedDate = Value.Date;
        CalendarPart.DisplayDate = Value.Date;
        _updatingText = false;
    }

    /// <summary>
    /// Keeps the calendar on the day grid. The themed template does not swap in
    /// the year and decade views, so any other mode would show a header that
    /// disagrees with the grid under it.
    /// </summary>
    private void OnCalendarDisplayModeChanged(object sender, CalendarModeChangedEventArgs args)
    {
        if (CalendarPart.DisplayMode != CalendarMode.Month)
            CalendarPart.DisplayMode = CalendarMode.Month;
    }

    private void OnCalendarDatePicked(object sender, SelectionChangedEventArgs args)
    {
        if (_updatingText || CalendarPart.SelectedDate is not { } date)
            return;

        // The calendar owns the date only; the time stays as typed.
        Value = date.Date + Value.TimeOfDay;
        PopupToggle.IsChecked = false;
    }

    private void OnTextKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Enter or Key.Return))
            return;

        CommitText();
        args.Handled = true;
    }

    private void OnTextLostFocus(object sender, RoutedEventArgs args) => CommitText();

    /// <summary>Puts the old value back when the text is not a date and time at all.</summary>
    private void CommitText()
    {
        if (_updatingText)
            return;

        if (DateTime.TryParseExact(
                TextPart.Text.Trim(),
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            Value = parsed;
        }

        ShowValue(Value);
    }
}
