using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Timetable.Core;

namespace Timetable.Desktop;

/// <summary>Talks to GitHub: finds the newest release, downloads its installer and starts it.</summary>
public static class AppUpdater
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
#if WIN7
        // .NET Framework on Windows 7 does not use TLS 1.2 (the only kind GitHub accepts) unless told to.
        System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
#endif
        // GitHub refuses requests that have no User-Agent.
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Timetable-Updater");
        return client;
    }

    /// <summary>The version of the running app. The build stamps it in; a local build has 1.0.0.</summary>
    public static Version CurrentVersion
    {
        get
        {
            var text = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            return UpdateChecker.TryParseVersion(text) ?? new Version(1, 0, 0);
        }
    }

    /// <summary>
    /// The installer leaves an uninstaller (unins000.exe) next to the app. The single-file download has none,
    /// and must not be "updated" by installing a second copy into Program Files.
    /// </summary>
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    public static async Task<UpdateInfo> GetLatestAsync()
    {
        var json = await Http.GetStringAsync(UpdateChecker.ReleaseApiUrl);
        return UpdateChecker.ParseRelease(json);
    }

    /// <summary>Downloads the installer into the temp folder and returns its path. <paramref name="onPercent"/> gets 0..100.</summary>
    public static async Task<string> DownloadInstallerAsync(UpdateInfo update, Action<int> onPercent)
    {
        var path = Path.Combine(Path.GetTempPath(), UpdateChecker.InstallerFileName);

        // Fetched first, so a release without a usable checksum is refused before a 45 MB download.
        var checksumList = await Http.GetStringAsync(update.ChecksumsUrl);
        var expectedHash = UpdateChecker.ExpectedHash(checksumList, UpdateChecker.InstallerFileName);

        using (var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using var source = await response.Content.ReadAsStreamAsync();
            using var target = File.Create(path);

            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await target.WriteAsync(buffer, 0, read);
                done += read;
                if (update.InstallerSize > 0)
                    onPercent((int)(done * 100 / update.InstallerSize));
            }
        }

        // A cut connection must not leave a half-file that we then run as administrator.
        if (new FileInfo(path).Length != update.InstallerSize)
        {
            File.Delete(path);
            throw new IOException("Yuklab olingan fayl to'liq emas.");
        }

        // The file must be exactly the one the build published.
        string actualHash;
        using (var file = File.OpenRead(path))
        using (var sha = SHA256.Create())
            actualHash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        if (!UpdateChecker.HashMatches(actualHash, expectedHash))
        {
            File.Delete(path);
            throw new InvalidDataException("Yuklab olingan fayl tekshiruvdan o'tmadi (nusxa buzilgan), shuning uchun o'chirildi.");
        }
        return path;
    }

    /// <summary>
    /// Starts the installer (Windows asks for administrator permission). It closes this app, installs over the
    /// old version and opens the app again. Returns false if the user said "No" to the permission question.
    /// </summary>
    public static bool StartInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installerPath, "/SILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS")
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return false;
        }
    }
}
