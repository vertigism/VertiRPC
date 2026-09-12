using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VertiRPC.ViewModels;

namespace VertiRPC;

public partial class MainWindow : Window
{
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmNcLButtonDblClk = 0x00A3;

    /// <summary>The hit-test code for the title bar's icon, where the system menu lives.</summary>
    private const int HtSysMenu = 3;

    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE: paints the title bar to match the theme.</summary>
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, in int value, int size);

    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.ShowWindowRequested += (_, _) => Restore();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.PinkTheme))
                ApplyTitleBarTheme();
        };
    }

    /// <summary>Set when the app is really quitting, so closing reaches the desktop.</summary>
    public bool AllowClose { get; set; }

    public void Restore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnSourceInitialized(EventArgs args)
    {
        base.OnSourceInitialized(args);

        // The title bar is outside WPF's reach, so the icon click that toggles
        // the pink theme has to come off the window's message loop.
        ((HwndSource)PresentationSource.FromVisual(this)!).AddHook(OnWindowMessage);

        ApplyTitleBarTheme();
    }

    /// <summary>
    /// Asks the desktop manager for a dark title bar, so the window frame is not
    /// a bright strip above a dark form. The pink palette is light, and keeps
    /// the default frame.
    /// </summary>
    private void ApplyTitleBarTheme()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        var useDark = _viewModel.PinkTheme ? 0 : 1;
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, in useDark, sizeof(int));
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        if (AllowClose)
        {
            base.OnClosing(args);
            return;
        }

        // Closing hides to the tray; Exit on the tray menu is the way out.
        args.Cancel = true;
        Hide();
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Check the message first: wParam carries values too wide for Int32 on
        // other messages, and only these two carry a hit-test code at all.
        if (message is not (WmNcLButtonDown or WmNcLButtonDblClk) || wParam.ToInt64() != HtSysMenu)
            return IntPtr.Zero;

        switch (message)
        {
            case WmNcLButtonDown:
                _viewModel.ToggleThemeCommand.Execute(null);
                handled = true;
                break;

            // Swallowed so a double click cannot trigger the system menu's
            // default close action and send the window to the tray.
            case WmNcLButtonDblClk:
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }
}
