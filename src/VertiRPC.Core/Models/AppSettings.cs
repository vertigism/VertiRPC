using System.Text.Json.Serialization;

namespace VertiRPC.Models;

/// <summary>
/// Everything persisted to %APPDATA%\VertiRPC\config.json. Mutable by design:
/// the view model binds straight to these properties rather than keeping a
/// second copy of the same state.
/// </summary>
public sealed class AppSettings
{
    public string ClientId { get; set; } = string.Empty;

    public ActivityType ActivityType { get; set; } = ActivityType.Playing;

    public bool RunOnStartup { get; set; } = false;

    public bool AutoConnect { get; set; } = false;

    public bool PinkTheme { get; set; } = false;

    /// <summary>First presence line, shown under the activity name.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Second presence line.</summary>
    public string State { get; set; } = string.Empty;

    public TimestampSettings Timestamp { get; set; } = new();

    public ActivityAssets Assets { get; set; } = new();

    // Discord allows at most two activity buttons, so the slots are fixed.
    public ActivityButton Button1 { get; set; } = new();

    public ActivityButton Button2 { get; set; } = new();
}

public sealed class TimestampSettings
{
    public TimestampMode Mode { get; set; } = TimestampMode.AppStart;

    public DateTimeOffset? CustomStart { get; set; }

    /// <summary>Null means the presence has no end time, so no countdown.</summary>
    public DateTimeOffset? CustomEnd { get; set; }
}

public sealed class ActivityAssets
{
    public string LargeImage { get; set; } = string.Empty;

    public string LargeText { get; set; } = string.Empty;

    public string SmallImage { get; set; } = string.Empty;

    public string SmallText { get; set; } = string.Empty;

    /// <summary>Derived, so it has no business in the config file.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        LargeImage.Length == 0
        && LargeText.Length == 0
        && SmallImage.Length == 0
        && SmallText.Length == 0;
}

public sealed class ActivityButton
{
    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>Discord rejects a button missing either half, so both must be set.</summary>
    [JsonIgnore]
    public bool IsComplete => Label.Length > 0 && Url.Length > 0;
}
