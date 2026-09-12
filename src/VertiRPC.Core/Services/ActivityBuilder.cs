using VertiRPC.Models;

namespace VertiRPC.Services;

/// <summary>
/// Turns the user's settings into the activity Discord is sent. Pure apart from
/// the clock, which is injected so every timestamp mode is testable.
/// </summary>
public sealed class ActivityBuilder
{
    private readonly TimeProvider _time;
    private readonly DateTimeOffset _appStart;

    public ActivityBuilder(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _appStart = _time.GetLocalNow();
    }

    public DiscordActivity Build(AppSettings settings) => new()
    {
        Type = (int)settings.ActivityType,
        Details = NullIfEmpty(settings.Details),
        State = NullIfEmpty(settings.State),
        Timestamps = BuildTimestamps(settings.Timestamp),
        Assets = BuildAssets(settings.Assets),
        Buttons = BuildButtons(settings.Button1, settings.Button2),
    };

    private DiscordTimestamps BuildTimestamps(TimestampSettings timestamp)
    {
        var now = _time.GetLocalNow();

        return timestamp.Mode switch
        {
            TimestampMode.LastUpdate => new DiscordTimestamps { Start = Seconds(now) },
            TimestampMode.LocalTime => new DiscordTimestamps { Start = Seconds(Midnight(now)) },
            TimestampMode.Custom => new DiscordTimestamps
            {
                Start = Seconds(timestamp.CustomStart ?? now),
                End = timestamp.CustomEnd is { } end ? Seconds(end) : null,
            },
            _ => new DiscordTimestamps { Start = Seconds(_appStart) },
        };
    }

    /// <summary>
    /// Midnight at the start of the current local day. Built from the offset in
    /// force at midnight rather than the offset right now, so the elapsed time
    /// still reads as a clock on the days the DST change lands.
    /// </summary>
    private DateTimeOffset Midnight(DateTimeOffset now)
    {
        var startOfDay = now.Date;
        return new DateTimeOffset(startOfDay, _time.LocalTimeZone.GetUtcOffset(startOfDay));
    }

    private static DiscordAssets? BuildAssets(ActivityAssets assets) =>
        assets.IsEmpty
            ? null
            : new DiscordAssets
            {
                LargeImage = NullIfEmpty(assets.LargeImage),
                LargeText = NullIfEmpty(assets.LargeText),
                SmallImage = NullIfEmpty(assets.SmallImage),
                SmallText = NullIfEmpty(assets.SmallText),
            };

    /// <summary>
    /// Keeps only the buttons with both a label and a URL, since Discord
    /// rejects the whole activity over a half-filled one.
    /// </summary>
    private static IReadOnlyList<DiscordButton>? BuildButtons(params ActivityButton[] buttons)
    {
        var complete = buttons
            .Where(button => button.IsComplete)
            .Select(button => new DiscordButton { Label = button.Label, Url = button.Url })
            .ToList();

        return complete.Count == 0 ? null : complete;
    }

    private static long Seconds(DateTimeOffset value) => value.ToUnixTimeSeconds();

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
