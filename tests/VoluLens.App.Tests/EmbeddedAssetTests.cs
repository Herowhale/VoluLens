namespace VoluLens.App.Tests;

public sealed class EmbeddedAssetTests
{
    [Fact]
    public void IndexHtml_ContainsRequiredViewsAndBridge()
    {
        var html = EmbeddedAssets.ReadIndexHtml();

        Assert.Contains("view-overview", html);
        Assert.Contains("view-scan", html);
        Assert.Contains("view-results", html);
        Assert.Contains("view-review", html);
        Assert.Contains("view-report", html);
        Assert.Contains("window.chrome.webview.postMessage", html);
        Assert.Contains("start-scan", html);
        Assert.Contains("cancel-scan", html);
        Assert.Contains("export-report", html);
        Assert.Contains("recycle-partial", html);
        Assert.Contains("data.totalBytes", html);
        Assert.Contains("result-group", html);
        Assert.Contains("group-select", html);
        Assert.Contains("review-ack", html);
        Assert.Contains("guided-cleanup", html);
        Assert.Contains("workspace-dock", html);
        Assert.DoesNotContain("Unclassified", html);
        Assert.DoesNotContain("System Storage Analysis", html);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__LUCIDE_LIBRARY__", html);
    }
}
