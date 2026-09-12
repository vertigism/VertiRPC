using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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

    // The size the Qt build asked for. Qt's resize() sets the client area,
    // where WPF's Width and Height cover the frame too, so the frame is
    // measured and added rather than eating into the form.
    private const double ClientWidth = 500;
    private const double ClientHeight = 750;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, in int value, int size);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

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
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.AddHook(OnWindowMessage);

        ApplyTitleBarTheme();
        SizeToClientArea(source);
    }

    /// <summary>
    /// Grows the window by however much its frame takes, so the form itself is
    /// the intended size, then centres what results on the work area.
    /// </summary>
    private void SizeToClientArea(HwndSource source)
    {
        if (!GetWindowRect(source.Handle, out var outer) || !GetClientRect(source.Handle, out var client))
            return;

        var toDevice = source.CompositionTarget!.TransformToDevice;
        var frameWidth = ((outer.Right - outer.Left) - client.Right) / toDevice.M11;
        var frameHeight = ((outer.Bottom - outer.Top) - client.Bottom) / toDevice.M22;

        Width = ClientWidth + frameWidth;
        Height = ClientHeight + frameHeight;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
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

    /// <summary>
    /// Swallows Tab so it does not walk the form. Leaving it to keyboard
    /// navigation sent focus straight out of the window, since the form is one
    /// container with nothing after it.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs args)
    {
        if (args.Key is Key.Tab)
        {
            args.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(args);
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
