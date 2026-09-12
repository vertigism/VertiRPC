namespace VertiRPC.Services;

/// <summary>Well-known locations VertiRPC writes to.</summary>
public static class AppPaths
{
    public const string AppName = "VertiRPC";

    public static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName);

    public static string ConfigFile { get; } = Path.Combine(ConfigDirectory, "config.json");
}
