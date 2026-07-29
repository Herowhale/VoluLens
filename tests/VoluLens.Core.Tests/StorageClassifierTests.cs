using VoluLens.Core;

namespace VoluLens.Core.Tests;

public sealed class StorageClassifierTests
{
    [Theory]
    [InlineData(@"C:\Users\A\AppData\Local\Temp", RiskLevel.Safe,
        "临时文件", CleanupMode.Recycle, GuidanceTarget.None)]
    [InlineData(@"C:\Users\A\AppData\Local\Google\Chrome\User Data\Default\Cache",
        RiskLevel.Safe, "应用缓存", CleanupMode.Recycle, GuidanceTarget.None)]
    [InlineData(@"C:\Users\A\.nuget\packages", RiskLevel.Safe,
        "开发缓存", CleanupMode.Recycle, GuidanceTarget.None)]
    [InlineData(@"C:\Users\A\Downloads", RiskLevel.Review,
        "下载内容", CleanupMode.ReviewRecycle, GuidanceTarget.None)]
    [InlineData(@"C:\Users\A\Documents\WeChat Files", RiskLevel.Review,
        "用户文档", CleanupMode.ReviewRecycle, GuidanceTarget.None)]
    [InlineData(@"C:\Users\A\Videos", RiskLevel.Review,
        "媒体内容", CleanupMode.ReviewRecycle, GuidanceTarget.None)]
    [InlineData(@"C:\Users\A\AppData\Roaming\Example", RiskLevel.Review,
        "应用数据", CleanupMode.ReviewRecycle, GuidanceTarget.None)]
    [InlineData(@"D:\Unsorted", RiskLevel.Review,
        "其他待识别", CleanupMode.ReviewRecycle, GuidanceTarget.None)]
    [InlineData(@"D:\Program Files\Example", RiskLevel.Protected,
        "已安装应用", CleanupMode.Guided, GuidanceTarget.AppsSettings)]
    [InlineData(@"C:\Windows\WinSxS", RiskLevel.Protected,
        "Windows 系统", CleanupMode.Guided, GuidanceTarget.StorageSettings)]
    public void Classify_AssignsChineseCategoryAndCleanupMetadata(
        string path,
        RiskLevel risk,
        string category,
        CleanupMode cleanupMode,
        GuidanceTarget guidanceTarget)
    {
        var result = new StorageClassifier(@"C:\Users\A").Classify(path);

        Assert.Equal(risk, result.Risk);
        Assert.Equal(category, result.Category);
        Assert.Equal(cleanupMode, result.CleanupMode);
        Assert.Equal(guidanceTarget, result.GuidanceTarget);
        Assert.DoesNotContain("Unclassified", result.Reason);
    }

    [Fact]
    public void Classify_DoesNotTrustNestedLookalikeAppDataPath()
    {
        var result = new StorageClassifier().Classify(
            @"C:\Users\A\Archive\AppData\Local\Temp");

        Assert.Equal(RiskLevel.Review, result.Risk);
        Assert.Equal("其他待识别", result.Category);
    }

    [Theory]
    [InlineData(@"C:\Users\A\Documents\temp")]
    [InlineData(@"C:\Users\A\Desktop\cache")]
    public void Classify_PersonalDataWinsOverCacheLikeNames(string path)
    {
        var result = new StorageClassifier(@"C:\Users\A").Classify(path);

        Assert.Equal(RiskLevel.Review, result.Risk);
        Assert.Equal("用户文档", result.Category);
    }

    [Fact]
    public void Classify_AnchorsKnownLocationsToConfiguredUserRoot()
    {
        var constructor = typeof(StorageClassifier).GetConstructor([typeof(string)]);

        Assert.NotNull(constructor);
        var classifier = Assert.IsType<StorageClassifier>(
            constructor.Invoke([@"D:\Profiles\Alice"]));
        Assert.Equal(
            RiskLevel.Safe,
            classifier.Classify(@"D:\Profiles\Alice\AppData\Local\Temp").Risk);
        Assert.Equal(
            RiskLevel.Review,
            classifier.Classify(@"D:\Profiles\Alice\Archive\AppData\Local\Temp").Risk);
    }
}
