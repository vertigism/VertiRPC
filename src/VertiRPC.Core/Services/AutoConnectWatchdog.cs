namespace VertiRPC.Services;

/// <summary>What the caller should do in response to a watchdog observation.</summary>
public enum WatchdogAction
{
    None,

    /// <summary>Discord is there and has no presence from us yet.</summary>
    Push,

    /// <summary>Discord has stayed gone long enough to call the connection dead.</summary>
    Drop,
}

/// <summary>
/// Decides when auto-connect should push a presence or let go of the pipe.
/// Holding the counter here rather than in a timer callback keeps the rule
/// testable without waiting on a clock.
/// </summary>
public sealed class AutoConnectWatchdog(int missesBeforeDrop = 2)
{
    private int _misses;

    /// <param name="discordAvailable">Whether a Discord IPC pipe is currently present.</param>
    /// <param name="presencePushed">Whether our presence is currently live.</param>
    public WatchdogAction Observe(bool discordAvailable, bool presencePushed)
    {
        if (discordAvailable)
        {
            _misses = 0;
            return presencePushed ? WatchdogAction.None : WatchdogAction.Push;
        }

        // Discord drops its pipe briefly whenever a client disconnects, so one
        // miss is not proof that it actually closed.
        _misses++;
        if (_misses < missesBeforeDrop || !presencePushed)
            return WatchdogAction.None;

        _misses = 0;
        return WatchdogAction.Drop;
    }

    public void Reset() => _misses = 0;
}
