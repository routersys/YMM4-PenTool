using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Controls
{
    public class UpdateCheckPanel : UserControl, IPropertyEditorControl
    {
        private static bool _updateCheckCompleted = false;
        private static readonly HttpClient _httpClient = new();
        private static string _settingsFilePath = "";
        private static string _ignoredVersion = "";
        private const string CurrentVersion = "2.0.0";

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public UpdateCheckPanel()
        {
            Visibility = Visibility.Collapsed;
            Loaded += OnLoaded;

            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YMM4-PenTool", CurrentVersion));
            }

            if (string.IsNullOrEmpty(_settingsFilePath))
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var pluginDir = Path.GetDirectoryName(assembly.Location);
                    if (pluginDir != null)
                    {
                        _settingsFilePath = Path.Combine(pluginDir, "PenToolSettings.json");
                        LoadSettings();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"設定ファイルのパス取得中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private static async Task CheckForUpdatesAsync()
        {
            if (_updateCheckCompleted) return;
            _updateCheckCompleted = true;

            try
            {
                var response = await _httpClient.GetAsync("https://api.github.com/repos/routersys/YMM4-PenTool/releases/latest");
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("tag_name", out var tagNameElement))
                {
                    string latestVersionTag = tagNameElement.GetString() ?? "";
                    string latestVersionStr = latestVersionTag.StartsWith("v") ? latestVersionTag.Substring(1) : latestVersionTag;

                    if (IsNewVersionAvailable(CurrentVersion, latestVersionStr))
                    {
                        if (latestVersionStr == _ignoredVersion) return;

                        var message = $"新しいバージョンのPenToolプラグインが利用可能です。\n\n" +
                                      $"現在のバージョン: v{CurrentVersion}\n" +
                                      $"最新バージョン: v{latestVersionStr}\n\n" +
                                      $"ダウンロードページを開きますか？\n\n" +
                                      $"（「いいえ」を選択すると、このバージョン(v{latestVersionStr})の通知は表示されなくなります）";

                        var result = MessageBox.Show(message, "PenTool - 更新通知", MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo("https://github.com/routersys/YMM4-PenTool/releases/latest") { UseShellExecute = true });
                        }
                        else
                        {
                            _ignoredVersion = latestVersionStr;
                            SaveSettings();
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"更新の確認中にネットワークエラーが発生しました。\n{ex.Message}", "更新確認エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"更新情報の解析中にエラーが発生しました。\n{ex.Message}", "更新確認エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"予期せぬエラーが発生しました。\n{ex.Message}", "更新確認エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static bool IsNewVersionAvailable(string currentVersionStr, string newVersionStr)
        {
            try
            {
                var currentParts = currentVersionStr.Split('.');
                var newParts = newVersionStr.Split('.');

                int length = Math.Max(currentParts.Length, newParts.Length);
                for (int i = 0; i < length; i++)
                {
                    int current = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;
                    int latest = i < newParts.Length ? int.Parse(newParts[i]) : 0;

                    if (latest > current) return true;
                    if (latest < current) return false;
                }
                return false;
            }
            catch (FormatException ex)
            {
                MessageBox.Show($"バージョン番号の形式が無効です。\n{ex.Message}", "バージョン比較エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private class PluginSettings
        {
            public string IgnoredVersion { get; set; } = "";
        }

        private static void LoadSettings()
        {
            if (!File.Exists(_settingsFilePath)) return;
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<PluginSettings>(json);
                if (settings != null)
                {
                    _ignoredVersion = settings.IgnoredVersion;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定ファイルの読み込みに失敗しました。\n{ex.Message}", "設定エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SaveSettings()
        {
            try
            {
                var settings = new PluginSettings { IgnoredVersion = _ignoredVersion };
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定ファイルの保存に失敗しました。\n{ex.Message}", "設定エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}