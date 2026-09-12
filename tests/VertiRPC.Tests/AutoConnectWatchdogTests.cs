using VertiRPC.Services;

namespace VertiRPC.Tests;

public class AutoConnectWatchdogTests
{
    [Fact]
    public void Observe_PushesOnceDiscordAppears()
    {
        var watchdog = new AutoConnectWatchdog();

        Assert.Equal(WatchdogAction.Push, watchdog.Observe(discordAvailable: true, presencePushed: false));
    }

    [Fact]
    public void Observe_StaysQuietWhileThePresenceIsAlreadyLive()
    {
        var watchdog = new AutoConnectWatchdog();

        Assert.Equal(WatchdogAction.None, watchdog.Observe(discordAvailable: true, presencePushed: true));
    }

    [Fact]
    public void Observe_IgnoresASingleMiss()
    {
        var watchdog = new AutoConnectWatchdog();

        // Discord drops its pipe for a moment whenever a client disconnects.
        Assert.Equal(WatchdogAction.None, watchdog.Observe(discordAvailable: false, presencePushed: true));
    }

    [Fact]
    public void Observe_DropsAfterTwoConsecutiveMisses()
    {
        var watchdog = new AutoConnectWatchdog();

        watchdog.Observe(discordAvailable: false, presencePushed: true);

        Assert.Equal(WatchdogAction.Drop, watchdog.Observe(discordAvailable: false, presencePushed: true));
    }

    [Fact]
    public void Observe_ForgetsEarlierMissesOnceDiscordIsBack()
    {
        var watchdog = new AutoConnectWatchdog();

        watchdog.Observe(discordAvailable: false, presencePushed: true);
        watchdog.Observe(discordAvailable: true, presencePushed: true);

        Assert.Equal(WatchdogAction.None, watchdog.Observe(discordAvailable: false, presencePushed: true));
    }

    [Fact]
    public void Observe_DoesNotDropWhenNothingWasPushed()
    {
        var watchdog = new AutoConnectWatchdog();

        watchdog.Observe(discordAvailable: false, presencePushed: false);

        Assert.Equal(WatchdogAction.None, watchdog.Observe(discordAvailable: false, presencePushed: false));
    }

    [Fact]
    public void Observe_RepushesAfterADrop()
    {
        var watchdog = new AutoConnectWatchdog();

        watchdog.Observe(discordAvailable: false, presencePushed: true);
        watchdog.Observe(discordAvailable: false, presencePushed: true);

        Assert.Equal(WatchdogAction.Push, watchdog.Observe(discordAvailable: true, presencePushed: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("not-a-number")]
    [InlineData("1234567890123456789012")]
    [InlineData("12345678901234567 ")]
    public void IsValidClientId_RejectsAnythingThatIsNotASnowflake(string clientId) =>
        Assert.False(PresenceService.IsValidClientId(clientId));

    [Theory]
    [InlineData("12345678901234567")]
    [InlineData("1234567890123456789")]
    [InlineData("12345678901234567890")]
    public void IsValidClientId_AcceptsSnowflakeLengths(string clientId) =>
        Assert.True(PresenceService.IsValidClientId(clientId));
}
