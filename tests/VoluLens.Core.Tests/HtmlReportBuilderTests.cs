using VoluLens.Core;

namespace VoluLens.Core.Tests;

public sealed class HtmlReportBuilderTests
{
    [Fact]
    public void Build_ProducesOfflineReadOnlyReport()
    {
        var html = new HtmlReportBuilder().Build(SampleResult(), redactUserPath: true);

        Assert.Contains("VOLULENS 存储分析报告", html);
        Assert.Contains("安全 · 临时文件", html);
        Assert.Contains("受保护 · Windows 系统", html);
        Assert.DoesNotContain(@"C:\Users\Alice", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("window.chrome.webview", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DISM /Online /Cleanup-Image /StartComponentCleanup", html);
        Assert.Contains("%USERPROFILE%", html);
    }

    [Fact]
    public void Build_EscapesFindingContent()
    {
        var result = SampleResult() with
        {
            Findings =
            [
                new("unsafe", @"C:\Users\Alice\<script>", "<script>alert(1)</script>", 10,
                    RiskLevel.Review, "用户文档", "请<strong>仔细</strong>核对。",
                    CleanupMode.ReviewRecycle)
            ]
        };

        var html = new HtmlReportBuilder().Build(result, redactUserPath: false);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("请&lt;strong&gt;仔细&lt;/strong&gt;核对。", html);
    }

    [Fact]
    public void Build_RedactsExplicitNonstandardUserRoot()
    {
        const string userRoot = @"D:\Profiles\Alice";
        var result = SampleResult() with
        {
            Roots = [userRoot],
            Findings =
            [
                new("cache", userRoot + @"\AppData\Local\Temp\cache", "cache", 10,
                    RiskLevel.Safe, "临时文件", "可以重新生成。", CleanupMode.Recycle)
            ],
            DeniedPaths = [userRoot + @"\Denied"]
        };

        var method = typeof(HtmlReportBuilder).GetMethod(
            "Build",
            [typeof(ScanResult), typeof(bool), typeof(string)]);

        Assert.NotNull(method);
        var html = Assert.IsType<string>(method.Invoke(
            new HtmlReportBuilder(),
            [result, true, userRoot]));

        Assert.DoesNotContain(userRoot, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", html);
    }

    private static ScanResult SampleResult() => new(
        [@"C:\Users\Alice"],
        [
            new("cache", @"C:\Users\Alice\AppData\Local\Temp\cache", "cache", 5_000_000,
                RiskLevel.Safe, "临时文件", "可以重新生成。", CleanupMode.Recycle),
            new("system", @"C:\Windows\WinSxS", "WinSxS", 20_000_000,
                RiskLevel.Protected, "Windows 系统", "此位置由 Windows 管理。",
                CleanupMode.Guided, GuidanceTarget.StorageSettings)
        ],
        [@"C:\Users\Alice\Denied"],
        25_000_000,
        DateTimeOffset.Parse("2026-07-29T08:00:00Z"),
        DateTimeOffset.Parse("2026-07-29T08:01:00Z"));
}
