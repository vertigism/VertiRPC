namespace VertiRPC.Models;

/// <summary>
/// Activity kinds Discord accepts over IPC. The member values are the numbers
/// that go on the wire, so casting to int is all the presence payload needs.
/// </summary>
public enum ActivityType
{
    Playing = 0,
    Listening = 2,
    Watching = 3,
    Competing = 5,
}

/// <summary>
/// Where the elapsed/remaining time shown under the presence comes from.
/// </summary>
public enum TimestampMode
{
    /// <summary>Counts up from the moment VertiRPC launched.</summary>
    AppStart,

    /// <summary>Counts up from the moment the presence was last pushed.</summary>
    LastUpdate,

    /// <summary>Counts up from midnight, so the elapsed time reads as a clock.</summary>
    LocalTime,

    /// <summary>Uses the start (and optional end) the user picked.</summary>
    Custom,
}
