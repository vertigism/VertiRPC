using System.Windows;
using VertiRPC.Services;
using VertiRPC.Themes;
using VertiRPC.ViewModels;

namespace VertiRPC;

public partial class App : Application
{
    // Global so one copy is enforced across every session on the machine, not
    // just within the one it started in.
    private const string SingleInstanceMutexName = @"Global\VertiRPC_SingleInstance_Mutex";
    private const string ActivationEventName = @"Global\VertiRPC_Activate";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private MainViewModel? _viewModel;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs args)
    {
        base.OnStartup(args);

        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            // Hand the running copy the click instead of starting a second one.
            if (EventWaitHandle.TryOpenExisting(ActivationEventName, out var running))
            {
                running.Set();
                running.Dispose();
            }

            Shutdown();
            return;
        }

        ListenForActivation();

        var settings = new SettingsService();
        var presence = new PresenceService();
        var startup = new StartupService();

        _viewModel = new MainViewModel(settings, presence, startup);
        _viewModel.ExitRequested += async (_, _) => await ShutdownAsync();

        ThemeManager.Apply(_viewModel.PinkTheme);

        _window = new MainWindow(_viewModel);
        MainWindow = _window;

        // Windows starts us with --tray at sign-in, where opening the window
        // would be an interruption; the tray icon still has to appear.
        if (args.Args.Contains(StartupService.TrayArgument, StringComparer.OrdinalIgnoreCase))
            _window.Tray.ForceCreate();
        else
            _window.Show();

        SessionEnding += async (_, _) => await ShutdownAsync();
    }

    protected override void OnExit(ExitEventArgs args)
    {
        _viewModel?.Dispose();
        _activationEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(args);
    }

    /// <summary>
    /// Waits for a second copy to report itself and brings the window back, so
    /// relaunching from the Start menu feels like reopening the app.
    /// </summary>
    private void ListenForActivation()
    {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);

        var listener = new Thread(() =>
        {
            while (_activationEvent.WaitOne())
            {
                try
                {
                    Dispatcher.Invoke(() => _window?.Restore());
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "VertiRPC activation listener",
        };

        listener.Start();
    }

    private async Task ShutdownAsync()
    {
        if (_window is not null)
        {
            _window.AllowClose = true;
            _window.Tray.Dispose();
        }

        if (_viewModel is not null)
            await _viewModel.ShutdownAsync();

        Shutdown();
    }
}
