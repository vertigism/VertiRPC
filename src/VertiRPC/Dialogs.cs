using System.Windows;

namespace VertiRPC;

/// <summary>
/// The message boxes the app reports outcomes through, as the Qt build did.
/// Windows plays the system sound tied to the icon, so picking the icon picks
/// the sound with it.
/// </summary>
internal static class Dialogs
{
    /// <summary>An action the user asked for went through. Plays the asterisk sound.</summary>
    internal static void Information(string caption, string message) =>
        Show(caption, message, MessageBoxImage.Information);

    /// <summary>Something did not work, but nothing is broken. Plays the exclamation sound.</summary>
    internal static void Warning(string caption, string message) =>
        Show(caption, message, MessageBoxImage.Warning);

    /// <summary>An outright failure. Plays the critical stop sound.</summary>
    internal static void Error(string caption, string message) =>
        Show(caption, message, MessageBoxImage.Error);

    private static void Show(string caption, string message, MessageBoxImage icon)
    {
        // Owned, so the dialog sits above the window and centres on it. A copy
        // started into the tray has no window on screen to own one, and falls
        // back to the centre of the display.
        var owner = Application.Current?.MainWindow;

        if (owner is { IsVisible: true })
            MessageBox.Show(owner, message, caption, MessageBoxButton.OK, icon);
        else
            MessageBox.Show(message, caption, MessageBoxButton.OK, icon);
    }
}
