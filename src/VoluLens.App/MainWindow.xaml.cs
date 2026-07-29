using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using VoluLens.Core;

namespace VoluLens.App;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppController _controller;

    public MainWindow()
    {
        InitializeComponent();
        var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var scanner = new StorageScanner(new SystemFileSystem(), new StorageClassifier(userRoot));
        _controller = new AppController(
            scanner,
            new RecycleBinService(),
            userRoot);
        _controller.ScanProgressChanged += progress => PostMessage(new("scan-progress", Data: progress));
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var webViewData = WebViewDataPath.Prepare(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: webViewData);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            Browser.NavigateToString(EmbeddedAssets.ReadIndexHtml());
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"VoluLens 无法启动 WebView2。\n\n{exception.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private async void OnWebMessageReceived(
        object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.WebMessageAsJson;
        var confirmation = GetConfirmationPrompt(json);
        if (confirmation is not null && MessageBox.Show(
                this,
                confirmation.Message,
                confirmation.Title,
                MessageBoxButton.OKCancel,
                confirmation.Icon,
                MessageBoxResult.Cancel) != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            var response = await _controller.HandleAsync(json);
            if (response.Type == "report-ready" && response.Data is ReportPayload report)
            {
                SaveReport(report);
                return;
            }

            PostMessage(response);
        }
        catch (Exception exception)
        {
            PostMessage(new("error", exception.Message));
        }
    }

    private void SaveReport(ReportPayload report)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 VoluLens HTML 报告",
            FileName = report.SuggestedFileName,
            DefaultExt = ".html",
            Filter = "HTML 报告 (*.html)|*.html",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, report.Html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        PostMessage(new("report-saved", Data: dialog.FileName));
    }

    private void PostMessage(AppResponse response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => PostMessage(response));
            return;
        }

        Browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static ConfirmationPrompt? GetConfirmationPrompt(string json)
    {
        try
        {
            var command = JsonSerializer.Deserialize<CommandEnvelope>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return command?.Type switch
            {
                "recycle" => new(
                    "确认清理",
                    "确认将所选项目移入回收站？需判断项目可能包含个人数据，请确认已完成审核。",
                    MessageBoxImage.Warning),
                "guided-cleanup" => new(
                    "确认打开处理入口",
                    "VoluLens 不会删除受保护项目，只会打开 Windows 或资源管理器中的正规处理入口。是否继续？",
                    MessageBoxImage.Information),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ConfirmationPrompt(
        string Title,
        string Message,
        MessageBoxImage Icon);

    private sealed record CommandEnvelope(string? Type);
}
