namespace VertiRPC.Services;

/// <summary>Well-known locations VertiRPC writes to.</summary>
public static class AppPaths
{
    public const string AppName = "VertiRPC";

    /// <summary>Owner and name, which both the repository link and the update check read.</summary>
    public const string Repository = "vertigism/VertiRPC";

    public const string RepositoryUrl = $"https://github.com/{Repository}";

    public static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName);

    public static string ConfigFile { get; } = Path.Combine(ConfigDirectory, "config.json");
}
