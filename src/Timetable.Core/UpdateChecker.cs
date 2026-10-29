using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Timetable.Core;

/// <summary>The newest release found on GitHub: its version and where to download the installer.</summary>
public sealed record UpdateInfo(Version Version, string InstallerUrl, long InstallerSize, string PageUrl);

/// <summary>
/// Works out from the GitHub "latest" release whether a newer version exists.
/// Only the reading of the answer lives here (so it can be tested); the download is done by the Desktop app.
///
/// The build workflow names the release "Latest build 1.0.N" and attaches Timetable-Setup.exe;
/// the same 1.0.N is stamped into the app, so the two can be compared.
/// </summary>
public static class UpdateChecker
{
    public const string ReleaseApiUrl = "https://api.github.com/repos/Mypatoff/timetable/releases/tags/latest";
    public const string InstallerFileName = "Timetable-Setup.exe";

    /// <summary>Reads GitHub's JSON answer for a release. Throws <see cref="InvalidDataException"/> if it is not usable.</summary>
    public static UpdateInfo ParseRelease(string json)
    {
        JsonObject release;
        try
        {
            release = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Unexpected answer from GitHub.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidDataException("Unexpected answer from GitHub.", ex);
        }

        var name = (string?)release["name"] ?? "";
        var pageUrl = (string?)release["html_url"] ?? "";
        var version = TryParseVersion(name)
                      ?? throw new InvalidDataException($"The release name \"{name}\" has no version number.");

        var installer = (release["assets"] as JsonArray)?
            .OfType<JsonObject>()
            .FirstOrDefault(a => (string?)a["name"] == InstallerFileName)
            ?? throw new InvalidDataException($"The release has no {InstallerFileName}.");

        // The installer is started with administrator rights, so it must come over HTTPS.
        var url = (string?)installer["browser_download_url"] ?? "";
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installer link is not an HTTPS link.");

        var size = (long?)installer["size"] ?? 0;
        return new UpdateInfo(version, url, size, pageUrl);
    }

    public static bool IsNewer(Version latest, Version current) => latest > current;

    /// <summary>Finds the first "1.0.12"-style number in the text. Handles "1.0.12+abc123" (the SDK adds the commit).</summary>
    public static Version? TryParseVersion(string text)
    {
        var match = Regex.Match(text, @"\d+\.\d+\.\d+");
        return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
    }
}
