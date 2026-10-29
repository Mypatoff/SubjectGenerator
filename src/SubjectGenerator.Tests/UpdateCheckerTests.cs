using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public class UpdateCheckerTests
{
    private const string InstallerUrl =
        "https://github.com/Mypatoff/timetable/releases/download/latest/SubjectGenerator-Setup.exe";

    private static string ReleaseJson(string name = "Latest build 1.0.42", string assetName = "SubjectGenerator-Setup.exe",
        string url = InstallerUrl) => $$"""
        {
          "name": "{{name}}",
          "html_url": "https://github.com/Mypatoff/timetable/releases/tag/latest",
          "assets": [
            { "name": "SubjectGenerator.exe", "size": 5, "browser_download_url": "https://github.com/x/SubjectGenerator.exe" },
            { "name": "{{assetName}}", "size": 123456, "browser_download_url": "{{url}}" }
          ]
        }
        """;

    [Fact]
    public void ParseRelease_ReadsVersionAndInstaller()
    {
        var info = UpdateChecker.ParseRelease(ReleaseJson());

        Assert.Equal(new Version(1, 0, 42), info.Version);
        Assert.Equal(InstallerUrl, info.InstallerUrl);
        Assert.Equal(123456, info.InstallerSize);
        Assert.EndsWith("/tag/latest", info.PageUrl);
    }

    [Fact]
    public void ParseRelease_NameWithoutVersion_Throws() =>
        Assert.Throws<InvalidDataException>(() => UpdateChecker.ParseRelease(ReleaseJson(name: "Latest build")));

    [Fact]
    public void ParseRelease_NoInstallerAsset_Throws() =>
        Assert.Throws<InvalidDataException>(() => UpdateChecker.ParseRelease(ReleaseJson(assetName: "Other.exe")));

    [Fact]
    public void ParseRelease_NonHttpsInstallerLink_Throws() =>
        Assert.Throws<InvalidDataException>(() =>
            UpdateChecker.ParseRelease(ReleaseJson(url: "http://example.com/SubjectGenerator-Setup.exe")));

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void ParseRelease_Garbage_Throws(string json) =>
        Assert.Throws<InvalidDataException>(() => UpdateChecker.ParseRelease(json));

    [Theory]
    [InlineData("1.0.43", "1.0.42", true)]
    [InlineData("1.0.42", "1.0.42", false)]
    [InlineData("1.0.41", "1.0.42", false)]
    [InlineData("1.0.100", "1.0.99", true)]
    public void IsNewer_ComparesNumbersNotText(string latest, string current, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current)));

    [Theory]
    [InlineData("1.0.12", "1.0.12")]
    [InlineData("1.0.12+5a5ffdf0c1", "1.0.12")]
    [InlineData("Latest build 1.0.7", "1.0.7")]
    public void TryParseVersion_FindsTheNumber(string text, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateChecker.TryParseVersion(text));

    [Fact]
    public void TryParseVersion_NoNumber_ReturnsNull() => Assert.Null(UpdateChecker.TryParseVersion("Latest build"));
}
