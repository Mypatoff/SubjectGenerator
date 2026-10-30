using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Timetable.Core;

/// <summary>The newest release found on GitHub: its version and where to download the installer.</summary>
public sealed record UpdateInfo(Version Version, string InstallerUrl, long InstallerSize, string ChecksumsUrl, string PageUrl);

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
#if WIN7
    // The Windows 7 build (.NET Framework) updates itself from its own installer.
    public const string InstallerFileName = "Timetable-Win7-Setup.exe";
#else
    public const string InstallerFileName = "Timetable-Setup.exe";
#endif

    /// <summary>"hash  file name" lines (sha256sum format) made by the build workflow.</summary>
    public const string ChecksumFileName = "SHA256SUMS.txt";

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

        var assets = (release["assets"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        var installer = assets.FirstOrDefault(a => (string?)a["name"] == InstallerFileName)
            ?? throw new InvalidDataException($"The release has no {InstallerFileName}.");
        var checksums = assets.FirstOrDefault(a => (string?)a["name"] == ChecksumFileName)
            ?? throw new InvalidDataException($"The release has no {ChecksumFileName}, so the download cannot be checked.");

        // The installer is started, so both files must come over HTTPS.
        var url = HttpsLink(installer, "installer");
        var checksumsUrl = HttpsLink(checksums, "checksum");

        var size = (long?)installer["size"] ?? 0;
        return new UpdateInfo(version, url, size, checksumsUrl, pageUrl);
    }

    private static string HttpsLink(JsonObject asset, string what)
    {
        var url = (string?)asset["browser_download_url"] ?? "";
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {what} link is not an HTTPS link.");
        return url;
    }

    /// <summary>
    /// The SHA-256 the checksum list gives for a file. Lines look like "hash  file name" (or "hash *file name").
    /// Throws <see cref="InvalidDataException"/> if the file is not listed.
    /// </summary>
    public static string ExpectedHash(string checksumList, string fileName)
    {
        foreach (var raw in checksumList.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length <= 65 || line[64] != ' ')
                continue;

            var hash = line[..64];
            var name = line[65..].TrimStart(' ', '*');
            if (name == fileName && hash.All(Uri.IsHexDigit))
                return hash.ToLowerInvariant();
        }
        throw new InvalidDataException($"{ChecksumFileName} does not list {fileName}.");
    }

    public static bool HashMatches(string actualHash, string expectedHash) =>
        string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

    public static bool IsNewer(Version latest, Version current) => latest > current;

    /// <summary>Finds the first "1.0.12"-style number in the text. Handles "1.0.12+abc123" (the SDK adds the commit).</summary>
    public static Version? TryParseVersion(string text)
    {
        var match = Regex.Match(text, @"\d+\.\d+\.\d+");
        return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
    }
}
