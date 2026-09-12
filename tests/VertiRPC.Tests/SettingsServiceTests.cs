using VertiRPC.Models;
using VertiRPC.Services;

namespace VertiRPC.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "VertiRPC.Tests", Guid.NewGuid().ToString("N"));

    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _service = new SettingsService(Path.Combine(_directory, "config.json"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Load_ReturnsDefaultsWhenNoFileExists()
    {
        var settings = _service.Load();

        Assert.Equal(string.Empty, settings.ClientId);
        Assert.Equal(ActivityType.Playing, settings.ActivityType);
        Assert.Equal(TimestampMode.AppStart, settings.Timestamp.Mode);
        Assert.Equal(string.Empty, settings.Button1.Label);
        Assert.Equal(string.Empty, settings.Button2.Url);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsEveryField()
    {
        var saved = new AppSettings
        {
            ClientId = "123456789012345678",
            ActivityType = ActivityType.Watching,
            RunOnStartup = true,
            AutoConnect = true,
            PinkTheme = true,
            Details = "Details",
            State = "State",
            Timestamp =
            {
                Mode = TimestampMode.Custom,
                CustomStart = new DateTimeOffset(2026, 5, 4, 3, 2, 1, TimeSpan.FromHours(2)),
                CustomEnd = new DateTimeOffset(2026, 5, 4, 4, 2, 1, TimeSpan.FromHours(2)),
            },
            Assets = { LargeImage = "large", LargeText = "large text", SmallImage = "small", SmallText = "small text" },
            Button1 = { Label = "One", Url = "https://one.example" },
        };

        Assert.True(_service.Save(saved));
        var loaded = _service.Load();

        Assert.Equal(saved.ClientId, loaded.ClientId);
        Assert.Equal(ActivityType.Watching, loaded.ActivityType);
        Assert.True(loaded.RunOnStartup);
        Assert.True(loaded.AutoConnect);
        Assert.True(loaded.PinkTheme);
        Assert.Equal("Details", loaded.Details);
        Assert.Equal("State", loaded.State);
        Assert.Equal(TimestampMode.Custom, loaded.Timestamp.Mode);
        Assert.Equal(saved.Timestamp.CustomStart, loaded.Timestamp.CustomStart);
        Assert.Equal(saved.Timestamp.CustomEnd, loaded.Timestamp.CustomEnd);
        Assert.Equal("large", loaded.Assets.LargeImage);
        Assert.Equal("small text", loaded.Assets.SmallText);
        Assert.Equal("One", loaded.Button1.Label);
        Assert.Equal("https://one.example", loaded.Button1.Url);
        Assert.Equal(string.Empty, loaded.Button2.Label);
    }

    [Fact]
    public void Save_WritesEnumsByName()
    {
        _service.Save(new AppSettings
        {
            ActivityType = ActivityType.Listening,
            Timestamp = { Mode = TimestampMode.LocalTime },
        });

        var json = File.ReadAllText(_service.FilePath);

        Assert.Contains("\"activityType\": \"Listening\"", json);
        Assert.Contains("\"mode\": \"LocalTime\"", json);
    }

    [Fact]
    public void Save_LeavesDerivedPropertiesOutOfTheFile()
    {
        _service.Save(new AppSettings());

        var json = File.ReadAllText(_service.FilePath);

        Assert.DoesNotContain("isEmpty", json);
        Assert.DoesNotContain("isComplete", json);
    }

    [Fact]
    public void Save_CreatesTheConfigDirectory()
    {
        var nested = new SettingsService(Path.Combine(_directory, "nested", "config.json"));

        Assert.True(nested.Save(new AppSettings()));
        Assert.True(File.Exists(nested.FilePath));
    }

    [Fact]
    public void Load_FallsBackToDefaultsAndSetsAsideAnUnreadableFile()
    {
        File.WriteAllText(_service.FilePath, "{ this is not json");

        var settings = _service.Load();

        Assert.Equal(string.Empty, settings.ClientId);
        Assert.True(File.Exists(_service.QuarantineFilePath));
        Assert.False(File.Exists(_service.FilePath));
    }

    [Fact]
    public void Load_FillsInSectionsAPartialFileLeftOut()
    {
        File.WriteAllText(_service.FilePath, """
            { "clientId": "  123  " }
            """);

        var settings = _service.Load();

        Assert.Equal("123", settings.ClientId);
        Assert.NotNull(settings.Assets);
        Assert.Equal(TimestampMode.AppStart, settings.Timestamp.Mode);
        Assert.NotNull(settings.Button1);
        Assert.NotNull(settings.Button2);
    }

    [Fact]
    public void Load_IgnoresPropertiesItDoesNotKnow()
    {
        File.WriteAllText(_service.FilePath, """
            { "clientId": "123", "somethingRetired": true }
            """);

        Assert.Equal("123", _service.Load().ClientId);
    }
}
