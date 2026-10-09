using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace WindowSizerWinUI;

public sealed partial class AboutPage : Page
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };
    private Uri? _releaseUri;

    public AboutPage()
    {
        InitializeComponent();
        Version current = CurrentVersion();
        VersionText.Text = $"当前版本：v{current.Major}.{current.Minor}.{current.Build}";
    }

    private static Version CurrentVersion()
    {
        Version version = typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        OpenReleaseButton.Visibility = Visibility.Collapsed;
        _releaseUri = null;
        UpdateStatus.Text = "正在检查更新…";

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/repos/RicardoMZH/WindowResizer/releases/latest");
            request.Headers.UserAgent.ParseAdd("WindowSizerWinUI");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            string? tag = document.RootElement.GetProperty("tag_name").GetString();
            if (tag is null || !Regex.IsMatch(tag, @"\Av[0-9]+\.[0-9]+\.[0-9]+\z") ||
                !Version.TryParse(tag[1..], out Version? latest))
            {
                UpdateStatus.Text = "最新发布版本的版本号格式无效。";
                return;
            }

            if (latest.CompareTo(CurrentVersion()) <= 0)
            {
                UpdateStatus.Text = $"已是最新版本（{tag}）。";
                return;
            }

            _releaseUri = new Uri(
                "https://github.com/RicardoMZH/WindowResizer/releases/tag/" +
                Uri.EscapeDataString(tag));
            UpdateStatus.Text = $"发现新版本 {tag}。打开发布页面选择下载文件。";
            OpenReleaseButton.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            UpdateStatus.Text = "检查更新失败，请确认网络连接后重试。";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_releaseUri is not null && !await Launcher.LaunchUriAsync(_releaseUri))
            UpdateStatus.Text = "无法打开发布页面。";
    }
}
