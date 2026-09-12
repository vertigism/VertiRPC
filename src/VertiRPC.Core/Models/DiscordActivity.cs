using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VertiRPC.Models;

/// <summary>
/// The activity object Discord expects inside SET_ACTIVITY. Property names are
/// Discord's, and anything left null is dropped from the payload rather than
/// sent as an empty value, which Discord rejects.
/// </summary>
public sealed record DiscordActivity
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("type")]
    public required int Type { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("timestamps")]
    public DiscordTimestamps? Timestamps { get; init; }

    [JsonPropertyName("assets")]
    public DiscordAssets? Assets { get; init; }

    [JsonPropertyName("buttons")]
    public IReadOnlyList<DiscordButton>? Buttons { get; init; }

    /// <summary>Renders the activity in the shape <see cref="Services.DiscordIpcClient"/> sends.</summary>
    public JsonObject ToJsonObject() =>
        JsonSerializer.SerializeToNode(this, PayloadOptions)!.AsObject();
}

/// <summary>Unix timestamps in seconds, which is what Discord's IPC surface takes.</summary>
public sealed record DiscordTimestamps
{
    [JsonPropertyName("start")]
    public long? Start { get; init; }

    [JsonPropertyName("end")]
    public long? End { get; init; }
}

public sealed record DiscordAssets
{
    [JsonPropertyName("large_image")]
    public string? LargeImage { get; init; }

    [JsonPropertyName("large_text")]
    public string? LargeText { get; init; }

    [JsonPropertyName("small_image")]
    public string? SmallImage { get; init; }

    [JsonPropertyName("small_text")]
    public string? SmallText { get; init; }
}

public sealed record DiscordButton
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
