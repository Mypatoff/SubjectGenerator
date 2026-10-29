using Timetable.Core;
using Xunit;

namespace Timetable.Tests;

public class UpdateCheckerTests
{
    private const string InstallerUrl =
        "https://github.com/Mypatoff/timetable/releases/download/latest/Timetable-Setup.exe";

    private const string ChecksumsUrl =
        "https://github.com/Mypatoff/timetable/releases/download/latest/SHA256SUMS.txt";

    private static string ReleaseJson(string name = "Latest build 1.0.42", string assetName = "Timetable-Setup.exe",
        string url = InstallerUrl, string checksumsName = "SHA256SUMS.txt", string checksumsUrl = ChecksumsUrl) => $$"""
        {
          "name": "{{name}}",
          "html_url": "https://github.com/Mypatoff/timetable/releases/tag/latest",
          "assets": [
            { "name": "Timetable.exe", "size": 5, "browser_download_url": "https://github.com/x/Timetable.exe" },
            { "name": "{{assetName}}", "size": 123456, "browser_download_url": "{{url}}" },
            { "name": "{{checksumsName}}", "size": 200, "browser_download_url": "{{checksumsUrl}}" }
          ]
        }
        """;

    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void ParseRelease_ReadsVersionAndInstaller()
    {
        var info = UpdateChecker.ParseRelease(ReleaseJson());

        Assert.Equal(new Version(1, 0, 42), info.Version);
        Assert.Equal(InstallerUrl, info.InstallerUrl);
        Assert.Equal(123456, info.InstallerSize);
        Assert.Equal(ChecksumsUrl, info.ChecksumsUrl);
        Assert.EndsWith("/tag/latest", info.PageUrl);
    }

    [Fact]
    public void ParseRelease_NoChecksumFile_Throws() =>
        Assert.Throws<InvalidDataException>(() => UpdateChecker.ParseRelease(ReleaseJson(checksumsName: "Other.txt")));

    [Fact]
    public void ParseRelease_NonHttpsChecksumLink_Throws() =>
        Assert.Throws<InvalidDataException>(() =>
            UpdateChecker.ParseRelease(ReleaseJson(checksumsUrl: "http://example.com/SHA256SUMS.txt")));

    [Theory]
    [InlineData($"{Hash}  Timetable-Setup.exe")]
    [InlineData($"{Hash} *Timetable-Setup.exe")]
    [InlineData($"ffff\n{Hash}  Timetable-Setup.exe\r\n{Hash}  Timetable.exe\r\n")]
    public void ExpectedHash_FindsTheInstallerLine(string list) =>
        Assert.Equal(Hash, UpdateChecker.ExpectedHash(list, "Timetable-Setup.exe"));

    [Fact]
    public void ExpectedHash_IsLowerCase() =>
        Assert.Equal(Hash, UpdateChecker.ExpectedHash($"{Hash.ToUpperInvariant()}  Timetable-Setup.exe", "Timetable-Setup.exe"));

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData($"{Hash}  Timetable.exe")]                          // another file's line
    [InlineData("zzzz456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  Timetable-Setup.exe")] // not hex
    public void ExpectedHash_MissingOrBroken_Throws(string list) =>
        Assert.Throws<InvalidDataException>(() => UpdateChecker.ExpectedHash(list, "Timetable-Setup.exe"));

    [Fact]
    public void HashMatches_IgnoresCase_AndRejectsOtherHashes()
    {
        Assert.True(UpdateChecker.HashMatches(Hash.ToUpperInvariant(), Hash));
        Assert.False(UpdateChecker.HashMatches(new string('0', 64), Hash));
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
            UpdateChecker.ParseRelease(ReleaseJson(url: "http://example.com/Timetable-Setup.exe")));

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
