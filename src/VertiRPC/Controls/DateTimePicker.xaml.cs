using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VertiRPC.Controls;

/// <summary>
/// A date and a time in one control, which WPF has no equivalent of: the date
/// comes from a <see cref="DatePicker"/> and the time from a 24-hour text box.
/// </summary>
public partial class DateTimePicker : UserControl
{
    private const string TimeFormat = "HH:mm:ss";

    private static readonly string[] AcceptedTimeFormats = ["HH:mm:ss", "H:mm:ss", "HH:mm", "H:mm"];

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(DateTime),
        typeof(DateTimePicker),
        new FrameworkPropertyMetadata(
            DateTime.Now,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    /// <summary>Guards against the control's own edits being read back as user input.</summary>
    private bool _updatingParts;

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
        _updatingParts = true;
        DatePart.SelectedDate = value.Date;
        TimePart.Text = value.ToString(TimeFormat, CultureInfo.CurrentCulture);
        _updatingParts = false;
    }

    private void OnDateChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updatingParts || DatePart.SelectedDate is not { } date)
            return;

        Value = date.Date + Value.TimeOfDay;
    }

    private void OnTimeKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Enter or Key.Return))
            return;

        CommitTime();
        args.Handled = true;
    }

    private void OnTimeLostFocus(object sender, RoutedEventArgs args) => CommitTime();

    /// <summary>
    /// Accepts <c>HH:mm</c> as well as <c>HH:mm:ss</c>, and puts the old time
    /// back when the text is not a time at all.
    /// </summary>
    private void CommitTime()
    {
        if (_updatingParts)
            return;

        if (DateTime.TryParseExact(
                TimePart.Text.Trim(),
                AcceptedTimeFormats,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            Value = Value.Date + parsed.TimeOfDay;
        }

        ShowValue(Value);
    }
}
