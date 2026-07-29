using VoluLens.Core;

namespace VoluLens.Core.Tests;

public sealed class FindingGrouperTests
{
    [Fact]
    public void Group_CombinesSameRiskAndCategoryAndOrdersByRiskThenSize()
    {
        ScanFinding[] findings =
        [
            Finding("edge", 20, RiskLevel.Safe, "应用缓存", CleanupMode.Recycle),
            Finding("chrome", 30, RiskLevel.Safe, "应用缓存", CleanupMode.Recycle),
            Finding("temp", 80, RiskLevel.Safe, "临时文件", CleanupMode.Recycle),
            Finding("docs", 90, RiskLevel.Review, "用户文档", CleanupMode.ReviewRecycle),
            Finding(
                "apps",
                100,
                RiskLevel.Protected,
                "已安装应用",
                CleanupMode.Guided,
                GuidanceTarget.AppsSettings)
        ];

        var groups = FindingGrouper.Group(findings);

        Assert.Collection(
            groups,
            group => Assert.Equal(
                (RiskLevel.Safe, "临时文件", 80L, 1),
                (group.Risk, group.Category, group.TotalBytes, group.Findings.Count)),
            group =>
            {
                Assert.Equal(
                    (RiskLevel.Safe, "应用缓存", 50L, 2),
                    (group.Risk, group.Category, group.TotalBytes, group.Findings.Count));
                Assert.Equal(["chrome", "edge"], group.Findings.Select(item => item.Id));
            },
            group => Assert.Equal(RiskLevel.Review, group.Risk),
            group => Assert.Equal(RiskLevel.Protected, group.Risk));
    }

    [Fact]
    public void Group_UsesRiskAndCategoryAsTheGroupKey()
    {
        var groups = FindingGrouper.Group(
        [
            Finding("safe", 20, RiskLevel.Safe, "共享分类", CleanupMode.Recycle),
            Finding("review", 30, RiskLevel.Review, "共享分类", CleanupMode.ReviewRecycle)
        ]);

        Assert.Equal(2, groups.Count);
        Assert.NotEqual(groups[0].Key, groups[1].Key);
    }

    private static ScanFinding Finding(
        string id,
        long bytes,
        RiskLevel risk,
        string category,
        CleanupMode cleanupMode,
        GuidanceTarget guidanceTarget = GuidanceTarget.None) =>
        new(
            id,
            $@"C:\Data\{id}",
            id,
            bytes,
            risk,
            category,
            $"{category}说明",
            cleanupMode,
            guidanceTarget);
}
