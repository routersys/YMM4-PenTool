using ExtendedPenTool.Localization;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace ExtendedPenTool.Services;

public static class UpdateCheckService
{
    private static bool updateCheckCompleted;
    private static readonly HttpClient httpClient = new();
    private static string settingsFilePath = string.Empty;
    private static string ignoredVersion = string.Empty;
    private static readonly string CurrentVersion = GetCurrentVersion();
    private const string RepoUrl = "https://api.github.com/repos/routersys/YMM4-PenTool/releases/latest";
    private const string ReleasesUrl = "https://github.com/routersys/YMM4-PenTool/releases/latest";

    private static string GetCurrentVersion()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is not null
                ? $"{Math.Max(0, version.Major)}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}"
                : "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    static UpdateCheckService()
    {
        if (httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("YMM4-ExtendedPenTool", CurrentVersion));
        }

        InitializeSettingsPath();
    }

    private static void InitializeSettingsPath()
    {
        try
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dir))
            {
                settingsFilePath = Path.Combine(dir, "ExtendedPenToolSettings.json");
                LoadSettings();
            }
        }
        catch
        {
        }
    }

    public static async Task CheckForUpdatesAsync()
    {
        if (updateCheckCompleted) return;
        updateCheckCompleted = true;

        try
        {
            var response = await httpClient.GetAsync(RepoUrl);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);

            if (!jsonDoc.RootElement.TryGetProperty("tag_name", out var tagElement)) return;

            var tag = tagElement.GetString() ?? string.Empty;
            var latestVersion = tag.StartsWith('v') ? tag[1..] : tag;

            if (!IsNewVersionAvailable(CurrentVersion, latestVersion)) return;
            if (latestVersion == ignoredVersion) return;

            var message = string.Format(Texts.UpdateNotificationMessage, CurrentVersion, latestVersion);

            var result = MessageBox.Show(message, Texts.UpdateNotificationTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
            }
            else
            {
                ignoredVersion = latestVersion;
                SaveSettings();
            }
        }
        catch
        {
        }
    }

    private static bool IsNewVersionAvailable(string current, string latest)
    {
        try
        {
            if (Version.TryParse(current, out var currentVersion) &&
                Version.TryParse(latest, out var latestVersion))
            {
                return latestVersion > currentVersion;
            }

            var currentParts = current.Split('.');
            var latestParts = latest.Split('.');
            var length = Math.Max(currentParts.Length, latestParts.Length);

            for (var i = 0; i < length; i++)
            {
                var c = i < currentParts.Length && int.TryParse(currentParts[i], out var cVal) ? cVal : 0;
                var l = i < latestParts.Length && int.TryParse(latestParts[i], out var lVal) ? lVal : 0;
                if (l > c) return true;
                if (l < c) return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private sealed class PluginSettings
    {
        public string IgnoredVersion { get; set; } = string.Empty;
    }

    private static void LoadSettings()
    {
        if (string.IsNullOrEmpty(settingsFilePath) || !File.Exists(settingsFilePath)) return;

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            var settings = JsonSerializer.Deserialize<PluginSettings>(json);
            if (settings is not null)
            {
                ignoredVersion = settings.IgnoredVersion;
            }
        }
        catch
        {
        }
    }

    private static void SaveSettings()
    {
        if (string.IsNullOrEmpty(settingsFilePath)) return;

        try
        {
            var settings = new PluginSettings { IgnoredVersion = ignoredVersion };
            File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
        }
    }
}
