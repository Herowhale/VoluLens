using VoluLens.Core;

namespace VoluLens.Core.Tests;

public sealed class StorageScannerTests
{
    [Fact]
    public async Task ScanAsync_AggregatesFilesAndRecordsDeniedFolders()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Data")
            .AddFile(@"C:\Data\a.bin", 10)
            .AddFile(@"C:\Data\b.bin", 20)
            .AddDirectory(@"C:\Data\Denied")
            .Deny(@"C:\Data\Denied");

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Data"], null, CancellationToken.None);

        Assert.Equal(30, result.TotalBytes);
        Assert.Contains(@"C:\Data\Denied", result.DeniedPaths);
    }

    [Fact]
    public async Task ScanAsync_ThrowsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new StorageScanner(new FakeFileSystem(), new StorageClassifier())
                .ScanAsync([@"C:\Data"], null, cancellation.Token));
    }

    [Fact]
    public async Task ScanAsync_SkipsReparsePointDirectories()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Data")
            .AddDirectory(@"C:\Data\Junction", isReparsePoint: true)
            .AddFile(@"C:\Data\Junction\loop.bin", 50);

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Data"], null, CancellationToken.None);

        Assert.Equal(0, result.TotalBytes);
        Assert.DoesNotContain(@"C:\Data\Junction", fileSystem.EnumeratedPaths);
    }

    [Fact]
    public async Task ScanAsync_RecordsPathTooLongDirectories()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Data")
            .AddDirectory(@"C:\Data\TooLong")
            .FailWithLongPath(@"C:\Data\TooLong");

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Data"], null, CancellationToken.None);

        Assert.Contains(@"C:\Data\TooLong", result.DeniedPaths);
    }

    [Fact]
    public async Task ScanAsync_ReportsCumulativeProgress()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Data")
            .AddFile(@"C:\Data\a.bin", 10)
            .AddFile(@"C:\Data\b.bin", 20);
        var progress = new RecordingProgress<ScanProgress>();

        await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Data"], progress, CancellationToken.None);

        Assert.Collection(
            progress.Values,
            item => Assert.Equal((1L, 10L), (item.FilesScanned, item.BytesScanned)),
            item => Assert.Equal((2L, 30L), (item.FilesScanned, item.BytesScanned)));
    }

    [Fact]
    public async Task ScanAsync_PromotesKnownNestedFolderToFinding()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\")
            .AddDirectory(@"C:\Users")
            .AddDirectory(@"C:\Users\A")
            .AddDirectory(@"C:\Users\A\AppData")
            .AddDirectory(@"C:\Users\A\AppData\Local")
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp")
            .AddFile(@"C:\Users\A\AppData\Local\Temp\cache.bin", 40);

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\"], null, CancellationToken.None);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(@"C:\Users\A\AppData\Local\Temp", finding.Path);
        Assert.Equal(RiskLevel.Safe, finding.Risk);
        Assert.Equal(CleanupMode.Recycle, finding.CleanupMode);
    }

    [Fact]
    public async Task ScanAsync_KeepsNestedSafeContentInSingleParentFinding()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp")
            .AddFile(@"C:\Users\A\AppData\Local\Temp\direct.bin", 10)
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp\Cache")
            .AddFile(@"C:\Users\A\AppData\Local\Temp\Cache\nested.bin", 20);

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Users\A\AppData\Local\Temp"], null, CancellationToken.None);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(@"C:\Users\A\AppData\Local\Temp", finding.Path);
        Assert.Equal(30, finding.Bytes);
    }

    [Fact]
    public async Task ScanAsync_DowngradesSafeFindingWhenChildCannotBeScanned()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp")
            .AddFile(@"C:\Users\A\AppData\Local\Temp\visible.bin", 10)
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp\Denied")
            .Deny(@"C:\Users\A\AppData\Local\Temp\Denied");

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Users\A\AppData\Local\Temp"], null, CancellationToken.None);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskLevel.Review, finding.Risk);
        Assert.Equal("扫描不完整", finding.Category);
        Assert.Equal(CleanupMode.ReviewRecycle, finding.CleanupMode);
    }

    [Fact]
    public async Task ScanAsync_DowngradesSafeFindingThatContainsPersonalData()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp")
            .AddFile(@"C:\Users\A\AppData\Local\Temp\cache.bin", 10)
            .AddDirectory(@"C:\Users\A\AppData\Local\Temp\Documents")
            .AddFile(@"C:\Users\A\AppData\Local\Temp\Documents\thesis.docx", 20);

        var constructor = typeof(StorageClassifier).GetConstructor([typeof(string)]);
        Assert.NotNull(constructor);
        var classifier = Assert.IsType<StorageClassifier>(
            constructor.Invoke([@"C:\Users\A"]));
        var result = await new StorageScanner(
                fileSystem,
                classifier)
            .ScanAsync([@"C:\Users\A\AppData\Local\Temp"], null, CancellationToken.None);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(30, finding.Bytes);
        Assert.Equal(RiskLevel.Review, finding.Risk);
        Assert.Equal("包含不同风险内容", finding.Category);
        Assert.Equal(CleanupMode.ReviewRecycle, finding.CleanupMode);
    }

    [Fact]
    public async Task ScanAsync_ContinuesWhenDirectoryDisappears()
    {
        var fileSystem = new FakeFileSystem()
            .AddDirectory(@"C:\Data")
            .AddDirectory(@"C:\Data\Gone")
            .FailWithIoError(@"C:\Data\Gone")
            .AddDirectory(@"C:\Data\Stable")
            .AddFile(@"C:\Data\Stable\kept.bin", 25);

        var result = await new StorageScanner(fileSystem, new StorageClassifier())
            .ScanAsync([@"C:\Data"], null, CancellationToken.None);

        Assert.Equal(25, result.TotalBytes);
        Assert.Contains(@"C:\Data\Gone", result.DeniedPaths);
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }
}
