namespace VertiRPC.Tests;

/// <summary>A clock the tests pin to a fixed instant and time zone.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset now, TimeZoneInfo? zone = null) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } = zone ?? TimeZoneInfo.Utc;
}
