using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VertiRPC.Models;
using VertiRPC.Services;
using VertiRPC.Themes;

namespace VertiRPC.ViewModels;

/// <summary>
/// The window's state. Properties read and write one <see cref="AppSettings"/>
/// instance rather than copying values in and out of it, so there is no second
/// copy to fall out of step.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public const string RepositoryUrl = "https://github.com/vertigism/VertiRPC";

    private static readonly TimeSpan DiscordPollInterval = TimeSpan.FromSeconds(5);

    private readonly SettingsService _settingsService;
    private readonly PresenceService _presence;
    private readonly StartupService _startup;
    private readonly AutoConnectWatchdog _watchdog = new();
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _discordTimer;
    private readonly DispatcherTimer _midnightTimer;

    /// <summary>Kept out of the settings: the end time stays editable while switched off.</summary>
    private DateTime _customEnd;

    private bool _customEndEnabled;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    public MainViewModel(SettingsService settingsService, PresenceService presence, StartupService startup)
    {
        _settingsService = settingsService;
        _presence = presence;
        _startup = startup;
        _settings = settingsService.Load();

        // The custom start doubles as the UI's value, so give it one up front.
        _settings.Timestamp.CustomStart ??= new DateTimeOffset(DateTime.Now);
        _customEndEnabled = _settings.Timestamp.CustomEnd is not null;
        _customEnd = _settings.Timestamp.CustomEnd?.LocalDateTime ?? CustomStart.AddHours(1);

        // Windows is the authority on whether the startup entry exists; the file
        // can disagree if the user removed it from Task Manager or Settings.
        _settings.RunOnStartup = _startup.IsEnabled();

        _discordTimer = new DispatcherTimer { Interval = DiscordPollInterval };
        _discordTimer.Tick += OnDiscordTick;

        _midnightTimer = new DispatcherTimer();
        _midnightTimer.Tick += OnMidnightRollover;

        if (AutoConnect)
            StartWatching();
    }

    /// <summary>Raised when the tray asks for the window back.</summary>
    public event EventHandler? ShowWindowRequested;

    /// <summary>Raised when the tray asks the app to quit for real.</summary>
    public event EventHandler? ExitRequested;

    public static string Title { get; } =
        $"VertiRPC {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0"}";

    public static IReadOnlyList<ActivityType> ActivityTypes { get; } = Enum.GetValues<ActivityType>();

    public string ClientId
    {
        get => _settings.ClientId;
        set => SetProperty(_settings.ClientId, value, v => _settings.ClientId = v);
    }

    public ActivityType ActivityType
    {
        get => _settings.ActivityType;
        set => SetProperty(_settings.ActivityType, value, v => _settings.ActivityType = v);
    }

    public string Details
    {
        get => _settings.Details;
        set => SetProperty(_settings.Details, value, v => _settings.Details = v);
    }

    public string State
    {
        get => _settings.State;
        set => SetProperty(_settings.State, value, v => _settings.State = v);
    }

    public string LargeImage
    {
        get => _settings.Assets.LargeImage;
        set => SetProperty(_settings.Assets.LargeImage, value, v => _settings.Assets.LargeImage = v);
    }

    public string LargeText
    {
        get => _settings.Assets.LargeText;
        set => SetProperty(_settings.Assets.LargeText, value, v => _settings.Assets.LargeText = v);
    }

    public string SmallImage
    {
        get => _settings.Assets.SmallImage;
        set => SetProperty(_settings.Assets.SmallImage, value, v => _settings.Assets.SmallImage = v);
    }

    public string SmallText
    {
        get => _settings.Assets.SmallText;
        set => SetProperty(_settings.Assets.SmallText, value, v => _settings.Assets.SmallText = v);
    }

    public string Button1Label
    {
        get => _settings.Button1.Label;
        set => SetProperty(_settings.Button1.Label, value, v => _settings.Button1.Label = v);
    }

    public string Button1Url
    {
        get => _settings.Button1.Url;
        set => SetProperty(_settings.Button1.Url, value, v => _settings.Button1.Url = v);
    }

    public string Button2Label
    {
        get => _settings.Button2.Label;
        set => SetProperty(_settings.Button2.Label, value, v => _settings.Button2.Label = v);
    }

    public string Button2Url
    {
        get => _settings.Button2.Url;
        set => SetProperty(_settings.Button2.Url, value, v => _settings.Button2.Url = v);
    }

    public TimestampMode TimestampMode
    {
        get => _settings.Timestamp.Mode;
        set
        {
            if (!SetProperty(_settings.Timestamp.Mode, value, v => _settings.Timestamp.Mode = v))
                return;

            OnPropertyChanged(nameof(IsCustomTimestamp));
        }
    }

    public bool IsCustomTimestamp => TimestampMode == Models.TimestampMode.Custom;

    public DateTime CustomStart
    {
        get => _settings.Timestamp.CustomStart?.LocalDateTime ?? DateTime.Now;
        set => SetProperty(CustomStart, value, v => _settings.Timestamp.CustomStart = new DateTimeOffset(v));
    }

    public DateTime CustomEnd
    {
        get => _customEnd;
        set
        {
            if (SetProperty(ref _customEnd, value))
                ApplyCustomEnd();
        }
    }

    public bool CustomEndEnabled
    {
        get => _customEndEnabled;
        set
        {
            if (SetProperty(ref _customEndEnabled, value))
                ApplyCustomEnd();
        }
    }

    public bool RunOnStartup
    {
        get => _settings.RunOnStartup;
        set
        {
            if (!SetProperty(_settings.RunOnStartup, value, v => _settings.RunOnStartup = v))
                return;

            if (!_startup.SetEnabled(value))
                ShowStatus("Could not change the Windows startup entry.", isError: true);

            Save();
        }
    }

    public bool AutoConnect
    {
        get => _settings.AutoConnect;
        set
        {
            if (!SetProperty(_settings.AutoConnect, value, v => _settings.AutoConnect = v))
                return;

            if (value)
                StartWatching();
            else
                StopWatching();

            Save();
        }
    }

    public bool PinkTheme
    {
        get => _settings.PinkTheme;
        set
        {
            if (!SetProperty(_settings.PinkTheme, value, v => _settings.PinkTheme = v))
                return;

            ThemeManager.Apply(value);
            Save();
        }
    }

    /// <summary>Pushes the current settings to Discord, reporting the outcome in the window.</summary>
    [RelayCommand]
    public async Task UpdatePresenceAsync()
    {
        Save();
        await PushAsync(announce: true);
    }

    [RelayCommand]
    private void ToggleTheme() => PinkTheme = !PinkTheme;

    [RelayCommand]
    private static void OpenRepository() =>
        Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });

    [RelayCommand]
    private void ShowWindow() => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Saves, clears the presence and lets go of the pipe, for a real exit.</summary>
    public async Task ShutdownAsync()
    {
        StopWatching();
        _midnightTimer.Stop();
        Save();
        await _presence.ClearAsync();
        _presence.Disconnect();
    }

    public void Dispose()
    {
        _discordTimer.Tick -= OnDiscordTick;
        _midnightTimer.Tick -= OnMidnightRollover;
        _presence.Dispose();
    }

    private void ApplyCustomEnd() =>
        _settings.Timestamp.CustomEnd = _customEndEnabled ? new DateTimeOffset(_customEnd) : null;

    private void Save()
    {
        if (!_settingsService.Save(_settings))
            ShowStatus("Settings could not be saved.", isError: true);
    }

    private void StartWatching()
    {
        _discordTimer.Start();
        _watchdog.Reset();
        OnDiscordTick(this, EventArgs.Empty);
    }

    private void StopWatching()
    {
        _discordTimer.Stop();
        _watchdog.Reset();
    }

    private async void OnDiscordTick(object? sender, EventArgs args)
    {
        try
        {
            switch (_watchdog.Observe(PresenceService.IsDiscordRunning, _presence.IsPushed))
            {
                case WatchdogAction.Push:
                    await PushAsync(announce: false);
                    break;

                case WatchdogAction.Drop:
                    _presence.Disconnect();
                    ShowStatus("Discord closed. The presence will return when it reopens.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auto-connect tick failed: {ex.Message}");
        }
    }

    private async void OnMidnightRollover(object? sender, EventArgs args)
    {
        _midnightTimer.Stop();
        if (TimestampMode == Models.TimestampMode.LocalTime && _presence.IsPushed)
            await PushAsync(announce: false);
    }

    private async Task PushAsync(bool announce)
    {
        var status = await _presence.PushAsync(_settings);

        if (status is PresenceStatus.Updated)
        {
            ScheduleMidnightRefresh();
            if (announce)
                ShowStatus("Rich Presence updated.");
            else
                ShowStatus("Connected to Discord.");
            return;
        }

        // A silent auto-connect attempt should not nag: Discord simply is not
        // ready yet, and the watchdog will try again.
        if (!announce && status is PresenceStatus.DiscordUnavailable)
            return;

        ShowStatus(Describe(status), isError: true);
    }

    private static string Describe(PresenceStatus status) => status switch
    {
        PresenceStatus.MissingClientId => "Enter your Discord application's Client ID first.",
        PresenceStatus.InvalidClientId => "That Client ID is not a Discord application ID (17-20 digits).",
        PresenceStatus.DiscordUnavailable => "Could not reach Discord. Is the desktop app running?",
        PresenceStatus.SendFailed => "Discord rejected the presence. Check the Client ID, image keys and button URLs.",
        _ => string.Empty,
    };

    /// <summary>
    /// In local-time mode the elapsed time is meant to read as a clock, so the
    /// presence is pushed again just after midnight to restart it at zero.
    /// </summary>
    private void ScheduleMidnightRefresh()
    {
        _midnightTimer.Stop();
        if (TimestampMode != Models.TimestampMode.LocalTime)
            return;

        var now = DateTime.Now;
        _midnightTimer.Interval = now.Date.AddDays(1) - now + TimeSpan.FromSeconds(1);
        _midnightTimer.Start();
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}
