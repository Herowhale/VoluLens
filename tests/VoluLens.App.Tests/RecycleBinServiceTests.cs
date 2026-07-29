namespace VoluLens.App.Tests;

public sealed class RecycleBinServiceTests
{
    [Fact]
    public async Task MoveToRecycleBinAsync_UsesRecycleOperationForDirectory()
    {
        var platform = new FakeRecycleOperation { DirectoryExistsResult = true };

        await new RecycleBinService(platform)
            .MoveToRecycleBinAsync(@"F:\VoluLens-Test\cache", CancellationToken.None);

        Assert.Equal([@"F:\VoluLens-Test\cache"], platform.RecycledDirectories);
        Assert.Empty(platform.RecycledFiles);
    }

    [Fact]
    public async Task MoveToRecycleBinAsync_RejectsMissingPath()
    {
        var platform = new FakeRecycleOperation();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new RecycleBinService(platform)
                .MoveToRecycleBinAsync(@"F:\VoluLens-Test\missing", CancellationToken.None));

        Assert.Empty(platform.RecycledDirectories);
        Assert.Empty(platform.RecycledFiles);
    }

    private sealed class FakeRecycleOperation : IRecycleOperation
    {
        public bool DirectoryExistsResult { get; init; }
        public bool FileExistsResult { get; init; }
        public List<string> RecycledDirectories { get; } = [];
        public List<string> RecycledFiles { get; } = [];

        public bool DirectoryExists(string path) => DirectoryExistsResult;
        public bool FileExists(string path) => FileExistsResult;
        public void RecycleDirectory(string path) => RecycledDirectories.Add(path);
        public void RecycleFile(string path) => RecycledFiles.Add(path);
    }
}
