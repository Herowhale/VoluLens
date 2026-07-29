using System.Reflection;

namespace VoluLens.App.Tests;

public sealed class MainWindowSafetyTests
{
    [Theory]
    [InlineData(
        "{\"Type\":\"recycle\",\"findingIds\":[\"safe\"]}",
        "确认清理")]
    [InlineData(
        "{\"type\":\"guided-cleanup\",\"findingIds\":[\"protected\"]}",
        "确认打开处理入口")]
    public void GetConfirmationPrompt_RecognizesCommands(
        string json,
        string expectedTitle)
    {
        var method = typeof(MainWindow).GetMethod(
            "GetConfirmationPrompt",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var prompt = method.Invoke(null, [json]);
        Assert.NotNull(prompt);
        var title = prompt.GetType().GetProperty("Title")?.GetValue(prompt);
        Assert.Equal(expectedTitle, Assert.IsType<string>(title));
    }

    [Theory]
    [InlineData("{\"type\":\"open-path\",\"findingId\":\"safe\"}")]
    [InlineData("not-json")]
    [InlineData("{}")]
    public void GetConfirmationPrompt_IgnoresOtherOrInvalidCommands(string json)
    {
        var method = typeof(MainWindow).GetMethod(
            "GetConfirmationPrompt",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Null(method.Invoke(null, [json]));
    }

    [Theory]
    [InlineData(false, @"C:\Users\A\AppData\Local\VoluLens\WebView2Data")]
    [InlineData(true, @"F:\Portable\WebView2Data")]
    public void WebViewDataPath_UsesWritableLocation(
        bool portableWritable,
        string expected)
    {
        var type = typeof(MainWindow).Assembly.GetType("VoluLens.App.WebViewDataPath");
        var method = type?.GetMethod(
            "Resolve",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var path = Assert.IsType<string>(method.Invoke(
            null,
            [@"F:\Portable", @"C:\Users\A\AppData\Local", portableWritable]));
        Assert.Equal(expected, path);
    }
}
