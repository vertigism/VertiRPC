using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    private static readonly TimeSpan DiscordPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>How long a check stands before the next one is due.</summary>
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);

    /// <summary>Ticks often enough that a machine waking from sleep is not left stale.</summary>
    private static readonly TimeSpan UpdateTickInterval = TimeSpan.FromHours(1);

    private static readonly Version CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version!;

    private readonly SettingsService _settingsService;
    private readonly PresenceService _presence;
    private readonly StartupService _startup;
    private readonly AutoConnectWatchdog _watchdog = new();
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _discordTimer;
    private readonly DispatcherTimer _midnightTimer;
    private readonly DispatcherTimer _updateTimer;
    private readonly UpdateService _updates = new();

    /// <summary>Kept out of the settings: the end time stays editable while switched off.</summary>
    private DateTime _customEnd;

    private bool _customEndEnabled;

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

        _updateTimer = new DispatcherTimer { Interval = UpdateTickInterval };
        _updateTimer.Tick += OnUpdateTick;
        _updateTimer.Start();

        if (AutoConnect)
            StartWatching();
    }

    /// <summary>Raised when the tray asks for the window back.</summary>
    public event EventHandler? ShowWindowRequested;

    /// <summary>Raised when the tray asks the app to quit for real.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Read off the assembly, so the csproj stays the one place the version is
    /// written. A compiled assembly always carries one, hence no fallback.
    /// </summary>
    public static string Title { get; } = $"VertiRPC {CurrentVersion.ToString(3)}";

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
                Dialogs.Warning("Startup Settings Error", "Could not change the Windows startup entry.");

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
        Process.Start(new ProcessStartInfo(AppPaths.RepositoryUrl) { UseShellExecute = true });

    [RelayCommand]
    private void ShowWindow() => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asked for from the tray, so it reports finding nothing as well.</summary>
    [RelayCommand]
    private async Task CheckForUpdates() => await CheckForUpdatesAsync(announce: true);

    /// <summary>
    /// Offers the newer release, if there is one, and hands over to its installer
    /// when the user accepts.
    /// </summary>
    /// <param name="announce">
    /// Whether the user asked. A check of its own accord stays quiet unless there
    /// is something to accept, honours the interval, and does not re-offer a
    /// version already turned down.
    /// </param>
    public async Task CheckForUpdatesAsync(bool announce)
    {
        if (!announce
            && _settings.LastUpdateCheck is { } last
            && DateTimeOffset.Now - last < UpdateCheckInterval)
            return;

        _settings.LastUpdateCheck = DateTimeOffset.Now;
        Save();

        var update = await _updates.FindUpdateAsync(CurrentVersion);

        if (update is null)
        {
            if (announce)
                Dialogs.Information("No Update", $"VertiRPC {CurrentVersion.ToString(3)} is the latest version.");

            return;
        }

        if (!announce && _settings.SkippedUpdate == update.Version.ToString())
            return;

        var accepted = Dialogs.Question(
            "Update Available",
            $"VertiRPC {update.Version.ToString(3)} is available. You have {CurrentVersion.ToString(3)}."
            + Environment.NewLine + Environment.NewLine
            + "Download and install it now? VertiRPC will close while it updates, and start again afterwards.");

        if (!accepted)
        {
            // Remembered so the prompt does not return every day for a version
            // already turned down; a later one asks again.
            _settings.SkippedUpdate = update.Version.ToString();
            Save();
            return;
        }

        var installer = await _updates.DownloadAsync(update);

        if (installer is null)
        {
            Dialogs.Error("Update Failed", "The update could not be downloaded. Try again, or fetch it from the GitHub page.");
            return;
        }

        StartInstaller(installer);
    }

    /// <summary>
    /// Hands over to the installer and gets out of its way: it replaces the very
    /// files this process is running from, so it cannot run while they change.
    /// </summary>
    private void StartInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installerPath)
            {
                // Silent, but not invisible: a progress window, and the flag that
                // tells Setup to start VertiRPC again once it is finished.
                Arguments = "/SILENT /NORESTART /fromapp=1",
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            Debug.WriteLine($"Could not start the installer: {ex.Message}");
            Dialogs.Error("Update Failed", "The installer could not be started.");
            return;
        }

        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

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
        _updateTimer.Tick -= OnUpdateTick;
        _updateTimer.Stop();
        _updates.Dispose();
        _presence.Dispose();
    }

    private void ApplyCustomEnd() =>
        _settings.Timestamp.CustomEnd = _customEndEnabled ? new DateTimeOffset(_customEnd) : null;

    private void Save()
    {
        if (!_settingsService.Save(_settings))
            Dialogs.Warning("Settings Not Saved", "Your settings could not be written to disk.");
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

                // Silently: Discord going away is not something the user did,
                // and the presence returns on its own when it reopens.
                case WatchdogAction.Drop:
                    _presence.Disconnect();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auto-connect tick failed: {ex.Message}");
        }
    }

    private async void OnUpdateTick(object? sender, EventArgs args)
    {
        // Same reasoning as the midnight refresh: past the first await there is
        // nothing above this to catch anything.
        try
        {
            await CheckForUpdatesAsync(announce: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    private async void OnMidnightRollover(object? sender, EventArgs args)
    {
        _midnightTimer.Stop();

        // Caught here or not at all: the handler returns at the first await, so
        // anything thrown after it lands on the dispatcher and ends the process.
        try
        {
            if (TimestampMode == Models.TimestampMode.LocalTime && _presence.IsPushed)
                await PushAsync(announce: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Midnight refresh failed: {ex.Message}");
        }
    }

    /// <param name="announce">
    /// Whether the user asked for this push, and so is owed a dialog. The
    /// watchdog's own attempts pass false: it runs every few seconds, and a
    /// message box each time would make the app unusable.
    /// </param>
    private async Task PushAsync(bool announce)
    {
        var status = await _presence.PushAsync(_settings);

        if (status is PresenceStatus.Updated)
            ScheduleMidnightRefresh();

        if (!announce)
            return;

        switch (status)
        {
            case PresenceStatus.Updated:
                Dialogs.Information("Success", "Discord Rich Presence updated successfully!");
                break;

            case PresenceStatus.MissingClientId:
                Dialogs.Warning("Missing Client ID", "Please enter a valid Discord Application Client ID.");
                break;

            case PresenceStatus.InvalidClientId:
                Dialogs.Warning("Invalid Client ID", "That Client ID is not a Discord application ID (17-20 digits).");
                break;

            case PresenceStatus.DiscordUnavailable:
                Dialogs.Warning("Connection Error", "Could not reach Discord. Is the desktop app running?");
                break;

            case PresenceStatus.SendFailed:
                Dialogs.Error("Update Failed", "Discord rejected the presence. Check the Client ID, image keys and button URLs.");
                break;
        }
    }

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
}
