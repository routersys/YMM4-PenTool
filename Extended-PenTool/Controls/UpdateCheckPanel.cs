using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace ExtendedPenTool.Controls;

public sealed class UpdateCheckPanel : UserControl, IPropertyEditorControl
{
    private static bool updateCheckCompleted;
    private static readonly HttpClient httpClient = new();
    private static string settingsFilePath = "";
    private static string ignoredVersion = "";
    private const string CurrentVersion = "3.0.0";
    private const string RepoUrl = "https://api.github.com/repos/routersys/YMM4-PenTool/releases/latest";
    private const string ReleasesUrl = "https://github.com/routersys/YMM4-PenTool/releases/latest";

#pragma warning disable CS0067
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
#pragma warning restore CS0067

    public UpdateCheckPanel()
    {
        Visibility = Visibility.Collapsed;
        Loaded += OnLoaded;

        if (httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("YMM4-ExtendedPenTool", CurrentVersion));
        }

        if (string.IsNullOrEmpty(settingsFilePath))
        {
            InitializeSettingsPath();
        }
    }

    private static void InitializeSettingsPath()
    {
        try
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dir is not null)
            {
                settingsFilePath = Path.Combine(dir, "ExtendedPenToolSettings.json");
                LoadSettings();
            }
        }
        catch
        {
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync();

    private static async Task CheckForUpdatesAsync()
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

            var tag = tagElement.GetString() ?? "";
            var latestVersion = tag.StartsWith('v') ? tag[1..] : tag;

            if (!IsNewVersionAvailable(CurrentVersion, latestVersion)) return;
            if (latestVersion == ignoredVersion) return;

            var message =
                $"新しいバージョンのPenToolプラグインが利用可能です。\n\n" +
                $"現在のバージョン: v{CurrentVersion}\n" +
                $"最新バージョン: v{latestVersion}\n\n" +
                $"ダウンロードページを開きますか？\n\n" +
                $"（「いいえ」を選択すると、このバージョン(v{latestVersion})の通知は表示されなくなります）";

            var result = MessageBox.Show(message, "ExtendedPenTool - 更新通知",
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
            var currentParts = current.Split('.');
            var latestParts = latest.Split('.');
            var length = Math.Max(currentParts.Length, latestParts.Length);

            for (var i = 0; i < length; i++)
            {
                var c = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;
                var l = i < latestParts.Length ? int.Parse(latestParts[i]) : 0;
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
        public string IgnoredVersion { get; set; } = "";
    }

    private static void LoadSettings()
    {
        if (!File.Exists(settingsFilePath)) return;
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
