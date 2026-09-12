using VertiRPC.Services;

namespace VertiRPC.Tests;

public class UpdateServiceTests
{
    private static readonly Version Running = new(2, 0, 0, 0);

    private static string Release(string tag, params string[] assetNames)
    {
        var assets = string.Join(",", assetNames.Select(name =>
            $$"""{"name":"{{name}}","browser_download_url":"https://example.test/{{name}}"}"""));

        return $$"""{"tag_name":"{{tag}}","draft":false,"prerelease":false,"assets":[{{assets}}]}""";
    }

    [Theory]
    [InlineData("2.1.0", 2, 1, 0)]
    [InlineData("2.0.0", 2, 0, 0)]
    [InlineData("10.2.30", 10, 2, 30)]
    public void ParseTag_ReadsAPlainVersionTag(string tag, int major, int minor, int build)
    {
        Assert.Equal(new Version(major, minor, build), UpdateService.ParseTag(tag));
    }

    [Theory]
    [InlineData("v2.1.0")]
    [InlineData("V2.1.0")]
    public void ParseTag_StillReadsATagPushedWithTheOtherConvention(string tag)
    {
        Assert.Equal(new Version(2, 1, 0), UpdateService.ParseTag(tag));
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseTag_RejectsWhatIsNotAVersion(string? tag)
    {
        Assert.Null(UpdateService.ParseTag(tag));
    }

    [Fact]
    public void IsNewer_IgnoresTheAssemblysFourthComponent()
    {
        // The tag has three components and the assembly four, and a missing one
        // sorts below zero, so a plain comparison reads these as different.
        Assert.False(UpdateService.IsNewer(new Version(2, 0, 0), new Version(2, 0, 0, 0)));
    }

    [Theory]
    [InlineData("2.1.0")]
    [InlineData("2.0.1")]
    [InlineData("3.0.0")]
    public void ReadRelease_OffersANewerRelease(string tag)
    {
        var update = UpdateService.ReadRelease(Release(tag, "VertiRPC-9.9.9-Setup.exe"), Running);

        Assert.NotNull(update);
        Assert.Equal(UpdateService.ParseTag(tag), update.Version);
        Assert.Equal("VertiRPC-9.9.9-Setup.exe", update.FileName);
        Assert.StartsWith("https://example.test/", update.DownloadUrl);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("1.9.9")]
    public void ReadRelease_IgnoresWhatIsNotNewer(string tag)
    {
        Assert.Null(UpdateService.ReadRelease(Release(tag, "VertiRPC-1.0.0-Setup.exe"), Running));
    }

    [Fact]
    public void ReadRelease_PicksTheInstallerOutOfTheAssets()
    {
        var json = Release("2.1.0", "VertiRPC.pdb", "source.zip", "VertiRPC-2.1.0-Setup.exe");

        Assert.Equal("VertiRPC-2.1.0-Setup.exe", UpdateService.ReadRelease(json, Running)?.FileName);
    }

    [Fact]
    public void ReadRelease_IgnoresAReleaseWithNoInstaller()
    {
        Assert.Null(UpdateService.ReadRelease(Release("2.1.0", "source.zip"), Running));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("prerelease")]
    public void ReadRelease_IgnoresDraftsAndPreReleases(string flag)
    {
        var json = $$"""
            {"tag_name":"2.1.0","{{flag}}":true,
             "assets":[{"name":"VertiRPC-2.1.0-Setup.exe","browser_download_url":"https://example.test/s.exe"}]}
            """;

        Assert.Null(UpdateService.ReadRelease(json, Running));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"tag_name":"2.1.0"}""")]
    public void ReadRelease_SurvivesWhatItCannotRead(string json)
    {
        Assert.Null(UpdateService.ReadRelease(json, Running));
    }
}
