using VertiRPC.Models;

namespace VertiRPC.Services;

public enum PresenceStatus
{
    Updated,

    /// <summary>No client ID was entered.</summary>
    MissingClientId,

    /// <summary>The client ID is not a Discord application ID.</summary>
    InvalidClientId,

    /// <summary>Discord is not running, or refused the handshake.</summary>
    DiscordUnavailable,

    /// <summary>Connected, but Discord rejected or dropped the activity.</summary>
    SendFailed,
}

/// <summary>
/// Owns the IPC connection and turns settings into a live presence. Every call
/// runs the blocking pipe work off the caller's thread, and one at a time, so a
/// watchdog tick and a button press cannot interleave on the same pipe.
/// </summary>
public sealed class PresenceService(ActivityBuilder? builder = null) : IDisposable
{
    private readonly DiscordIpcClient _ipc = new();
    private readonly ActivityBuilder _builder = builder ?? new ActivityBuilder();
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Whether Discord currently holds a presence from us.</summary>
    public bool IsPushed { get; private set; }

    public static bool IsDiscordRunning => DiscordIpcClient.IsDiscordAvailable();

    public async Task<PresenceStatus> PushAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.ClientId.Length == 0)
            return PresenceStatus.MissingClientId;

        if (!IsValidClientId(settings.ClientId))
            return PresenceStatus.InvalidClientId;

        var activity = _builder.Build(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                if (!_ipc.Connect(settings.ClientId))
                {
                    IsPushed = false;
                    return PresenceStatus.DiscordUnavailable;
                }

                if (!_ipc.SetActivity(activity.ToJsonObject()))
                {
                    IsPushed = false;
                    return PresenceStatus.SendFailed;
                }

                IsPushed = true;
                return PresenceStatus.Updated;
            }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Clears the presence but keeps the pipe open.</summary>
    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                if (_ipc.IsConnected)
                    _ipc.ClearActivity();
                IsPushed = false;
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lets go of the pipe, for when Discord has gone away.</summary>
    public void Disconnect()
    {
        _ipc.Close();
        IsPushed = false;
    }

    /// <summary>Discord application IDs are snowflakes: 17 to 20 digits.</summary>
    public static bool IsValidClientId(string clientId) =>
        clientId.Length is >= 17 and <= 20 && clientId.All(char.IsAsciiDigit);

    public void Dispose()
    {
        _ipc.Dispose();
        _gate.Dispose();
    }
}
