using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace VertiRPC.Services;

public sealed class DiscordIpcClient : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;

    private NamedPipeClientStream? _pipe;
    private string? _connectedClientId;

    public bool IsConnected => _pipe is { IsConnected: true };

    public static bool IsDiscordAvailable()
    {
        try
        {
            return Directory.GetFiles(@"\\.\pipe\")
                .Any(name => Path.GetFileName(name).StartsWith("discord-ipc-", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool Connect(string clientId)
    {
        if (_pipe is { IsConnected: true } && _connectedClientId == clientId)
            return true;

        Close();

        for (var index = 0; index < 10; index++)
        {
            var pipeName = $"discord-ipc-{index}";
            try
            {
                var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(100);
                _pipe = pipe;

                var handshake = new JsonObject
                {
                    ["v"] = 1,
                    ["client_id"] = clientId,
                };

                if (Send(OpHandshake, handshake))
                {
                    var response = Read();
                    if (response?["evt"]?.GetValue<string>() == "ERROR")
                    {
                        Close();
                        continue;
                    }

                    _connectedClientId = clientId;
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
            {
                Close();
            }
        }

        return false;
    }

    public bool SetActivity(JsonObject? activity)
    {
        var payload = new JsonObject
        {
            ["cmd"] = "SET_ACTIVITY",
            ["args"] = new JsonObject
            {
                ["pid"] = Environment.ProcessId,
                ["activity"] = activity,
            },
            ["nonce"] = Guid.NewGuid().ToString(),
        };

        if (!Send(OpFrame, payload))
            return false;

        var response = Read();
        return response?["evt"]?.GetValue<string>() != "ERROR";
    }

    public bool ClearActivity() => SetActivity(null);

    public void Close()
    {
        _pipe?.Dispose();
        _pipe = null;
        _connectedClientId = null;
    }

    public void Dispose() => Close();

    private bool Send(int opcode, JsonNode payload)
    {
        if (_pipe is not { IsConnected: true })
            return false;

        try
        {
            var data = Encoding.UTF8.GetBytes(payload.ToJsonString());
            var header = new byte[8];
            BitConverter.GetBytes(opcode).CopyTo(header, 0);
            BitConverter.GetBytes(data.Length).CopyTo(header, 4);

            _pipe.Write(header, 0, header.Length);
            _pipe.Write(data, 0, data.Length);
            _pipe.Flush();
            return true;
        }
        catch (IOException)
        {
            Close();
            return false;
        }
    }

    private JsonNode? Read()
    {
        if (_pipe is not { IsConnected: true })
            return null;

        try
        {
            var header = ReadExact(8);
            if (header is null)
                return null;

            var length = BitConverter.ToInt32(header, 4);
            var payload = ReadExact(length);
            return payload is null ? null : JsonNode.Parse(payload);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private byte[]? ReadExact(int count)
    {
        if (_pipe is null)
            return null;

        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = _pipe.Read(buffer, offset, count - offset);
            if (read == 0)
                return null;
            offset += read;
        }
        return buffer;
    }
}
