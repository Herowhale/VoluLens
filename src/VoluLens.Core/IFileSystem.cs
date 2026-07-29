namespace VoluLens.Core;

public sealed record FileSystemEntry(
    string Path,
    bool IsDirectory,
    long Bytes,
    bool IsReparsePoint);

public interface IFileSystem
{
    IEnumerable<FileSystemEntry> EnumerateEntries(string path);
}

public interface IStorageScanner
{
    Task<ScanResult> ScanAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
