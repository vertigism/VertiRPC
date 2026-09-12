using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using VertiRPC.Models;

namespace VertiRPC.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. A missing, unreadable or corrupt
/// file never stops the app starting: it falls back to defaults, keeping the
/// bad file aside so the user can recover anything they typed.
/// </summary>
public sealed class SettingsService(string? filePath = null)
{
    private static readonly JsonSerializerOptions FileOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; } = filePath ?? AppPaths.ConfigFile;

    /// <summary>Where a file that failed to parse is moved before defaults are used.</summary>
    public string QuarantineFilePath => FilePath + ".invalid";

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
            return Normalize(new AppSettings());

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), FileOptions);
            return Normalize(settings ?? new AppSettings());
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Failed to load configuration: {ex.Message}");
            Quarantine();
            return Normalize(new AppSettings());
        }
    }

    /// <returns><c>false</c> if the file could not be written; the caller decides whether to tell the user.</returns>
    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            // Write beside the target and swap it in, so an interrupted save
            // cannot leave a half-written config behind.
            var tempFile = FilePath + ".tmp";
            File.WriteAllText(tempFile, JsonSerializer.Serialize(Normalize(settings), FileOptions));
            File.Move(tempFile, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Debug.WriteLine($"Failed to save configuration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fills in anything a hand-edited or partial file left out, and pads the
    /// button list to the fixed number of slots the UI shows.
    /// </summary>
    public static AppSettings Normalize(AppSettings settings)
    {
        settings.ClientId = settings.ClientId.Trim();
        settings.Details = settings.Details.Trim();
        settings.State = settings.State.Trim();
        settings.SkippedUpdate = settings.SkippedUpdate?.Trim() ?? string.Empty;

        settings.Timestamp ??= new TimestampSettings();

        settings.Assets ??= new ActivityAssets();
        settings.Assets.LargeImage = settings.Assets.LargeImage.Trim();
        settings.Assets.LargeText = settings.Assets.LargeText.Trim();
        settings.Assets.SmallImage = settings.Assets.SmallImage.Trim();
        settings.Assets.SmallText = settings.Assets.SmallText.Trim();

        settings.Button1 = TrimButton(settings.Button1);
        settings.Button2 = TrimButton(settings.Button2);

        return settings;

        static ActivityButton TrimButton(ActivityButton? button)
        {
            button ??= new ActivityButton();
            button.Label = button.Label.Trim();
            button.Url = button.Url.Trim();
            return button;
        }
    }

    private void Quarantine()
    {
        try
        {
            File.Move(FilePath, QuarantineFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Failed to set aside unreadable configuration: {ex.Message}");
        }
    }
}
