using VoluLens.Core;

namespace VoluLens.Core.Tests;

public sealed class SystemFileSystemTests
{
    [Fact]
    public void EnumerateEntries_ReturnsImmediateFilesAndDirectories()
    {
        var root = Path.Combine(AppContext.BaseDirectory, $"VoluLens-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "sample.bin"), [1, 2, 3]);
            Directory.CreateDirectory(Path.Combine(root, "nested"));

            var entries = new SystemFileSystem().EnumerateEntries(root).ToArray();

            Assert.Contains(entries, entry =>
                entry.Path == Path.Combine(root, "sample.bin") &&
                !entry.IsDirectory &&
                entry.Bytes == 3);
            Assert.Contains(entries, entry =>
                entry.Path == Path.Combine(root, "nested") &&
                entry.IsDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
