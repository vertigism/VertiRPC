using System.Diagnostics;
using Microsoft.Win32;

namespace VertiRPC.Services;

/// <summary>
/// Registers VertiRPC to launch with Windows, per user. The command points at
/// the installed executable and adds the tray switch, so a boot-time launch
/// starts quietly in the notification area instead of opening the window.
/// </summary>
public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Tells the app it was started by Windows rather than by the user.</summary>
    public const string TrayArgument = "--tray";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppPaths.AppName) is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Debug.WriteLine($"Failed to read the startup entry: {ex.Message}");
            return false;
        }
    }

    /// <returns><c>false</c> if the registry could not be written.</returns>
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
                key.SetValue(AppPaths.AppName, $"\"{ExecutablePath}\" {TrayArgument}", RegistryValueKind.String);
            else
                key.DeleteValue(AppPaths.AppName, throwOnMissingValue: false);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Debug.WriteLine($"Failed to write the startup entry: {ex.Message}");
            return false;
        }
    }

    private static string ExecutablePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, $"{AppPaths.AppName}.exe");
}
