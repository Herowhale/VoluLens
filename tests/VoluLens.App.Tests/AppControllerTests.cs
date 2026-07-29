using System.Text.Json;
using VoluLens.Core;

namespace VoluLens.App.Tests;

public sealed class AppControllerTests
{
    [Fact]
    public async Task HandleAsync_RejectsRecycleForUnknownFindingId()
    {
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["unknown"]}""");

        Assert.Equal("error", response.Type);
        Assert.Empty(recycleBin.Paths);
    }

    [Theory]
    [InlineData("protected")]
    [InlineData("outside")]
    public async Task HandleAsync_RejectsProtectedAndOutOfScopeRecycleFindings(string findingId)
    {
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            $$"""{"type":"recycle","findingIds":["{{findingId}}"]}""");

        Assert.Equal("error", response.Type);
        Assert.Empty(recycleBin.Paths);
    }

    [Fact]
    public async Task HandleAsync_RejectsReviewFindingWithoutAcknowledgement()
    {
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["review"]}""");

        Assert.Equal("error", response.Type);
        Assert.Empty(recycleBin.Paths);
    }

    [Fact]
    public async Task HandleAsync_RecyclesAcknowledgedReviewFinding()
    {
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["review"],"acknowledgedReviewIds":["review"]}""");

        Assert.Equal("recycle-complete", response.Type);
        Assert.Equal([@"C:\Users\A\Downloads"], recycleBin.Paths);
    }

    [Fact]
    public async Task HandleAsync_RecyclesSafeAllowlistedFinding()
    {
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["safe"]}""");

        Assert.Equal("recycle-complete", response.Type);
        Assert.Equal([@"C:\Users\A\AppData\Local\Temp\cache"], recycleBin.Paths);
    }

    [Fact]
    public async Task HandleAsync_RemovesRecycledFindingFromCurrentResult()
    {
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            new FakeRecycleBinService(),
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        await controller.HandleAsync("""{"type":"recycle","findingIds":["safe"]}""");
        var repeated = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["safe"]}""");
        var reportResponse = await controller.HandleAsync(
            """{"type":"export-report","redactUserPath":false}""");

        var report = Assert.IsType<ReportPayload>(reportResponse.Data);
        Assert.Equal("error", repeated.Type);
        Assert.DoesNotContain(@"C:\Users\A\AppData\Local\Temp\cache", report.Html);
    }

    [Fact]
    public async Task HandleAsync_RejectsOverlappingRecycleFindings()
    {
        var result = SampleResult() with
        {
            Findings =
            [
                new("parent", @"C:\Users\A\AppData\Local\Temp", "Temp", 100,
                    RiskLevel.Safe, "临时文件", "可重新生成", CleanupMode.Recycle),
                new("child", @"C:\Users\A\AppData\Local\Temp\cache", "cache", 50,
                    RiskLevel.Safe, "应用缓存", "可重新生成", CleanupMode.Recycle)
            ]
        };
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(result),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["parent","child"]}""");

        Assert.Equal("error", response.Type);
        Assert.Empty(recycleBin.Paths);
    }

    [Fact]
    public async Task HandleAsync_RejectsRecycleParentWithUnselectedDescendantFinding()
    {
        var result = SampleResult() with
        {
            Findings =
            [
                new("parent", @"C:\Users\A\AppData\Local\Temp", "Temp", 100,
                    RiskLevel.Safe, "临时文件", "可重新生成", CleanupMode.Recycle),
                new("child", @"C:\Users\A\AppData\Local\Temp\cache", "cache", 50,
                    RiskLevel.Safe, "应用缓存", "可重新生成", CleanupMode.Recycle)
            ]
        };
        var recycleBin = new FakeRecycleBinService();
        var controller = new AppController(
            new FakeScanner(result),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["parent"]}""");

        Assert.Equal("error", response.Type);
        Assert.Empty(recycleBin.Paths);
    }

    [Fact]
    public async Task HandleAsync_ReportsSuccessfulItemsWhenBatchRecyclePartiallyFails()
    {
        var firstPath = @"C:\Users\A\AppData\Local\Temp\one";
        var secondPath = @"C:\Users\A\AppData\Local\Temp\two";
        var result = SampleResult() with
        {
            Findings =
            [
                new("first", firstPath, "one", 100,
                    RiskLevel.Safe, "临时文件", "可重新生成", CleanupMode.Recycle),
                new("second", secondPath, "two", 50,
                    RiskLevel.Safe, "临时文件", "可重新生成", CleanupMode.Recycle)
            ],
            TotalBytes = 150
        };
        var recycleBin = new FakeRecycleBinService(secondPath);
        var controller = new AppController(
            new FakeScanner(result),
            recycleBin,
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"recycle","findingIds":["first","second"]}""");
        var reportResponse = await controller.HandleAsync(
            """{"type":"export-report","redactUserPath":false}""");

        Assert.Equal("recycle-partial", response.Type);
        var data = JsonSerializer.SerializeToElement(
            response.Data,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(["first"], data.GetProperty("findingIds")
            .EnumerateArray()
            .Select(item => item.GetString()));
        Assert.Equal(50, data.GetProperty("totalBytes").GetInt64());
        var report = Assert.IsType<ReportPayload>(reportResponse.Data);
        Assert.DoesNotContain(firstPath, report.Html);
        Assert.Contains(secondPath, report.Html);
    }

    [Fact]
    public async Task HandleAsync_RedactsConfiguredUserRootFromReport()
    {
        const string userRoot = @"D:\Profiles\Alice";
        var result = SampleResult() with
        {
            Roots = [userRoot],
            Findings =
            [
                new("safe", userRoot + @"\AppData\Local\Temp\cache", "cache", 100,
                    RiskLevel.Safe, "临时文件", "可重新生成", CleanupMode.Recycle)
            ]
        };
        var controller = new AppController(
            new FakeScanner(result),
            new FakeRecycleBinService(),
            userRoot);
        await controller.HandleAsync("""{"type":"start-scan","roots":["D:\\Profiles\\Alice"]}""");

        var response = await controller.HandleAsync(
            """{"type":"export-report","redactUserPath":true}""");

        var report = Assert.IsType<ReportPayload>(response.Data);
        Assert.DoesNotContain(userRoot, report.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", report.Html);
    }

    [Fact]
    public async Task HandleAsync_ListsAvailableDrives()
    {
        DriveSummary[] drives = [new("Work", @"F:\", 1_000, 400, 600)];
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            new FakeRecycleBinService(),
            @"F:\Users\A",
            driveProvider: new FakeDriveProvider(drives));

        var response = await controller.HandleAsync("""{"type":"list-drives"}""");

        Assert.Equal("drives", response.Type);
        Assert.Same(drives, response.Data);
    }

    [Fact]
    public async Task HandleAsync_OpensFindingFromCurrentScan()
    {
        var launcher = new FakePathLauncher();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            new FakeRecycleBinService(),
            @"C:\Users\A",
            pathLauncher: launcher);
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"open-path","findingId":"review"}""");

        Assert.Equal("open-complete", response.Type);
        Assert.Equal([@"C:\Users\A\Downloads"], launcher.Paths);
    }

    [Fact]
    public async Task HandleAsync_GuidesProtectedFindingWithoutRecyclingIt()
    {
        var recycleBin = new FakeRecycleBinService();
        var guidedLauncher = new FakeGuidedCleanupLauncher();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            recycleBin,
            @"C:\Users\A",
            guidedCleanupLauncher: guidedLauncher);
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\"]}""");

        var response = await controller.HandleAsync(
            """{"type":"guided-cleanup","findingIds":["protected"]}""");

        Assert.Equal("guidance-complete", response.Type);
        Assert.Empty(recycleBin.Paths);
        Assert.Equal(
            [(GuidanceTarget.StorageSettings, @"C:\Windows\WinSxS")],
            guidedLauncher.Requests);
    }

    [Theory]
    [InlineData("review")]
    [InlineData("unknown")]
    public async Task HandleAsync_RejectsNonProtectedOrUnknownGuidance(string findingId)
    {
        var guidedLauncher = new FakeGuidedCleanupLauncher();
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            new FakeRecycleBinService(),
            @"C:\Users\A",
            guidedCleanupLauncher: guidedLauncher);
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\"]}""");

        var response = await controller.HandleAsync(
            $$"""{"type":"guided-cleanup","findingIds":["{{findingId}}"],"target":"AppsSettings"}""");

        Assert.Equal("error", response.Type);
        Assert.Empty(guidedLauncher.Requests);
    }

    [Fact]
    public async Task HandleAsync_ExportsReadOnlyReportPayload()
    {
        var controller = new AppController(
            new FakeScanner(SampleResult()),
            new FakeRecycleBinService(),
            @"C:\Users\A");
        await controller.HandleAsync("""{"type":"start-scan","roots":["C:\\Users\\A"]}""");

        var response = await controller.HandleAsync(
            """{"type":"export-report","redactUserPath":true}""");

        var payload = Assert.IsType<ReportPayload>(response.Data);
        Assert.Equal("report-ready", response.Type);
        Assert.Contains("VOLULENS 存储分析报告", payload.Html);
        Assert.DoesNotContain("window.chrome.webview", payload.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_CancelsActiveScan()
    {
        var scanner = new BlockingScanner();
        var controller = new AppController(
            scanner,
            new FakeRecycleBinService(),
            @"F:\Users\A");
        var scanTask = controller.HandleAsync(
            """{"type":"start-scan","roots":["F:\\"]}""");
        await scanner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var cancelResponse = await controller.HandleAsync("""{"type":"cancel-scan"}""");
        if (cancelResponse.Type != "scan-cancel-requested")
        {
            scanner.Release.TrySetResult();
        }
        var scanResponse = await scanTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("scan-cancel-requested", cancelResponse.Type);
        Assert.Equal("scan-cancelled", scanResponse.Type);
    }

    [Fact]
    public async Task HandleAsync_ForwardsScannerProgress()
    {
        var controller = new AppController(
            new ProgressScanner(SampleResult()),
            new FakeRecycleBinService(),
            @"F:\Users\A");
        var updates = new List<ScanProgress>();
        controller.ScanProgressChanged += updates.Add;

        await controller.HandleAsync(
            """{"type":"start-scan","roots":["F:\\"]}""");

        var update = Assert.Single(updates);
        Assert.Equal(42, update.BytesScanned);
    }

    private static ScanResult SampleResult() => new(
        [@"C:\"],
        [
            new("safe", @"C:\Users\A\AppData\Local\Temp\cache", "cache", 100,
                RiskLevel.Safe, "临时文件", "可重新生成", CleanupMode.Recycle),
            new("review", @"C:\Users\A\Downloads", "Downloads", 200,
                RiskLevel.Review, "下载内容", "可能包含个人文件", CleanupMode.ReviewRecycle),
            new("protected", @"C:\Windows\WinSxS", "WinSxS", 300,
                RiskLevel.Protected, "Windows 系统", "由 Windows 管理",
                CleanupMode.Guided, GuidanceTarget.StorageSettings),
            new("outside", @"D:\Temp\cache", "cache", 400,
                RiskLevel.Safe, "临时文件", "不在扫描范围", CleanupMode.Recycle)
        ],
        [],
        1_000,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    private sealed class FakeScanner(ScanResult result) : IStorageScanner
    {
        public Task<ScanResult> ScanAsync(
            IReadOnlyList<string> roots,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeRecycleBinService(string? failPath = null) : IRecycleBinService
    {
        public List<string> Paths { get; } = [];

        public Task MoveToRecycleBinAsync(string path, CancellationToken cancellationToken)
        {
            if (path.Equals(failPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Simulated recycle failure.");
            }

            Paths.Add(path);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDriveProvider(IReadOnlyList<DriveSummary> drives) : IDriveProvider
    {
        public IReadOnlyList<DriveSummary> GetDrives() => drives;
    }

    private sealed class FakePathLauncher : IPathLauncher
    {
        public List<string> Paths { get; } = [];

        public void Open(string path) => Paths.Add(path);
    }

    private sealed class FakeGuidedCleanupLauncher : IGuidedCleanupLauncher
    {
        public List<(GuidanceTarget Target, string Path)> Requests { get; } = [];

        public void Open(GuidanceTarget target, string findingPath) =>
            Requests.Add((target, findingPath));
    }

    private sealed class BlockingScanner : IStorageScanner
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ScanResult> ScanAsync(
            IReadOnlyList<string> roots,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.WhenAny(
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
                Release.Task);
            cancellationToken.ThrowIfCancellationRequested();
            return SampleResult();
        }
    }

    private sealed class ProgressScanner(ScanResult result) : IStorageScanner
    {
        public Task<ScanResult> ScanAsync(
            IReadOnlyList<string> roots,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new(@"F:\sample.bin", 1, 1, 42));
            return Task.FromResult(result);
        }
    }
}
