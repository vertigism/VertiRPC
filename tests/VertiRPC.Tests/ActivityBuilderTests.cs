using VertiRPC.Models;
using VertiRPC.Services;

namespace VertiRPC.Tests;

public class ActivityBuilderTests
{
    // UTC+1, moving to UTC+2 for summer time, so the DST edge is exercisable.
    private static readonly TimeZoneInfo CentralEurope =
        TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");

    private static readonly DateTimeOffset Noon =
        new(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Build_MapsActivityTypeToDiscordValue()
    {
        var settings = new AppSettings { ActivityType = ActivityType.Competing };

        Assert.Equal(5, Build(settings).Type);
    }

    [Fact]
    public void Build_OmitsEmptyDetailsAndState()
    {
        var activity = Build(new AppSettings { Details = "", State = "" });

        Assert.Null(activity.Details);
        Assert.Null(activity.State);
        Assert.DoesNotContain("details", activity.ToJsonObject().ToJsonString());
    }

    [Fact]
    public void Build_OmitsAssetsWhenAllBlank()
    {
        Assert.Null(Build(new AppSettings()).Assets);
    }

    [Fact]
    public void Build_KeepsAssetsWhenAnyFieldIsSet()
    {
        var settings = new AppSettings { Assets = { LargeImage = "cover" } };

        var assets = Build(settings).Assets;

        Assert.NotNull(assets);
        Assert.Equal("cover", assets.LargeImage);
        Assert.Null(assets.SmallImage);
    }

    [Fact]
    public void Build_DropsButtonsMissingEitherHalf()
    {
        var settings = new AppSettings
        {
            Button1 = { Label = "Label only" },
            Button2 = { Label = "GitHub", Url = "https://github.com/vertigism/VertiRPC" },
        };

        var button = Assert.Single(Build(settings).Buttons!);
        Assert.Equal("GitHub", button.Label);
    }

    [Fact]
    public void Build_OmitsButtonsWhenNoneAreComplete()
    {
        Assert.Null(Build(new AppSettings()).Buttons);
    }

    [Fact]
    public void Build_AppStartModeKeepsTheLaunchTimeAsTheClockMovesOn()
    {
        var clock = new FakeTimeProvider(Noon, CentralEurope);
        var builder = new ActivityBuilder(clock);
        clock.Now = Noon.AddHours(3);

        var timestamps = builder.Build(new AppSettings()).Timestamps;

        Assert.Equal(Noon.ToUnixTimeSeconds(), timestamps!.Start);
        Assert.Null(timestamps.End);
    }

    [Fact]
    public void Build_LastUpdateModeUsesTheCurrentTime()
    {
        var clock = new FakeTimeProvider(Noon, CentralEurope);
        var builder = new ActivityBuilder(clock);
        clock.Now = Noon.AddHours(3);

        var settings = new AppSettings { Timestamp = { Mode = TimestampMode.LastUpdate } };

        Assert.Equal(clock.Now.ToUnixTimeSeconds(), builder.Build(settings).Timestamps!.Start);
    }

    [Fact]
    public void Build_LocalTimeModeStartsAtMidnight()
    {
        var settings = new AppSettings { Timestamp = { Mode = TimestampMode.LocalTime } };
        var midnight = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.FromHours(2));

        Assert.Equal(midnight.ToUnixTimeSeconds(), Build(settings).Timestamps!.Start);
    }

    [Fact]
    public void Build_LocalTimeModeUsesTheOffsetInForceAtMidnightOnDstDays()
    {
        // Clocks go forward at 02:00 on 2026-03-29, so midnight was still UTC+1
        // even though it is UTC+2 by the time the presence is built.
        var afterTheChange = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.FromHours(2));
        var midnight = new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.FromHours(1));
        var builder = new ActivityBuilder(new FakeTimeProvider(afterTheChange, CentralEurope));

        var settings = new AppSettings { Timestamp = { Mode = TimestampMode.LocalTime } };

        Assert.Equal(midnight.ToUnixTimeSeconds(), builder.Build(settings).Timestamps!.Start);
    }

    [Fact]
    public void Build_CustomModeUsesBothEndsWhenAnEndIsSet()
    {
        var start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var settings = new AppSettings
        {
            Timestamp =
            {
                Mode = TimestampMode.Custom,
                CustomStart = start,
                CustomEnd = start.AddHours(1),
            },
        };

        var timestamps = Build(settings).Timestamps;

        Assert.Equal(start.ToUnixTimeSeconds(), timestamps!.Start);
        Assert.Equal(start.AddHours(1).ToUnixTimeSeconds(), timestamps.End);
    }

    [Fact]
    public void Build_CustomModeOmitsTheEndWhenItIsNotSet()
    {
        var settings = new AppSettings
        {
            Timestamp = { Mode = TimestampMode.Custom, CustomStart = Noon },
        };

        var activity = Build(settings);

        Assert.Null(activity.Timestamps!.End);
        Assert.DoesNotContain("\"end\"", activity.ToJsonObject().ToJsonString());
    }

    [Fact]
    public void ToJsonObject_UsesDiscordFieldNames()
    {
        var settings = new AppSettings
        {
            ActivityType = ActivityType.Listening,
            Details = "A song",
            State = "An artist",
            Assets = { LargeImage = "cover", SmallText = "shuffle" },
            Button1 = { Label = "Listen", Url = "https://example.com" },
        };

        var json = Build(settings).ToJsonObject().ToJsonString();

        Assert.Contains("\"type\":2", json);
        Assert.Contains("\"large_image\":\"cover\"", json);
        Assert.Contains("\"small_text\":\"shuffle\"", json);
        Assert.Contains("\"buttons\":[{\"label\":\"Listen\",\"url\":\"https://example.com\"}]", json);
        Assert.DoesNotContain("large_text", json);
    }

    private static DiscordActivity Build(AppSettings settings) =>
        new ActivityBuilder(new FakeTimeProvider(Noon, CentralEurope)).Build(settings);
}
