using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VertiRPC.Services;

/// <summary>A release newer than the running build, and the installer to fetch.</summary>
public sealed record AvailableUpdate(Version Version, string DownloadUrl, string FileName);

/// <summary>
/// Asks GitHub whether a newer release exists and fetches its installer. The
/// reading of a release is kept separate from the fetching of one, so the part
/// with the rules in it can be tested without a network.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly string LatestReleaseUrl =
        $"https://api.github.com/repos/{AppPaths.Repository}/releases/latest";

    private readonly HttpClient _http;

    public UpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // GitHub refuses requests that do not name themselves.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppPaths.AppName}/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>
    /// The newer release, or null when there is none, the network is unreachable,
    /// or the release carries no installer. A check nobody asked for stays quiet.
    /// </summary>
    public async Task<AvailableUpdate?> FindUpdateAsync(Version current, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _http.GetStringAsync(LatestReleaseUrl, cancellationToken);
            return ReadRelease(json, current);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <returns>Where the installer was saved, or null if it could not be fetched.</returns>
    public async Task<string?> DownloadAsync(AvailableUpdate update, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetTempPath(), update.FileName);

        try
        {
            await using (var source = await _http.GetStreamAsync(update.DownloadUrl, cancellationToken))
            await using (var file = File.Create(path))
            {
                await source.CopyToAsync(file, cancellationToken);
            }

            return path;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Update download failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Picks the installer out of GitHub's latest-release JSON, if that release
    /// is newer than what is running. Drafts and pre-releases are not offered.
    /// </summary>
    public static AvailableUpdate? ReadRelease(string json, Version current)
    {
        JsonObject? release;
        try
        {
            release = JsonNode.Parse(json)?.AsObject();
        }
        catch (JsonException)
        {
            return null;
        }

        if (release is null
            || release["draft"]?.GetValue<bool>() == true
            || release["prerelease"]?.GetValue<bool>() == true)
            return null;

        if (ParseTag(release["tag_name"]?.GetValue<string>()) is not { } version || !IsNewer(version, current))
            return null;

        foreach (var asset in release["assets"]?.AsArray() ?? [])
        {
            var name = asset?["name"]?.GetValue<string>();
            var url = asset?["browser_download_url"]?.GetValue<string>();

            if (name is not null && url is not null
                && name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase))
                return new AvailableUpdate(version, url, name);
        }

        // A release with no installer is nothing this can offer to install.
        return null;
    }

    /// <summary>Release tags are "v2.1.0"; anything else is not one we can read.</summary>
    public static Version? ParseTag(string? tag) =>
        Version.TryParse(tag?.TrimStart('v', 'V'), out var version) ? version : null;

    /// <summary>
    /// Compares on major.minor.patch alone. An assembly version carries a fourth
    /// component where a tag does not, and a missing component sorts below zero,
    /// which would otherwise read 2.1.0 as older than 2.1.0.0.
    /// </summary>
    public static bool IsNewer(Version candidate, Version current) => Trim(candidate) > Trim(current);

    private static Version Trim(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    public void Dispose() => _http.Dispose();
}
