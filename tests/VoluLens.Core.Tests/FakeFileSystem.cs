using VoluLens.Core;

namespace VoluLens.Core.Tests;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, List<FileSystemEntry>> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _errors =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> EnumeratedPaths { get; } = [];

    public FakeFileSystem AddDirectory(string path, bool isReparsePoint = false)
    {
        AddToParent(new FileSystemEntry(path, true, 0, isReparsePoint));
        _entries.TryAdd(path, []);
        return this;
    }

    public FakeFileSystem AddFile(string path, long bytes)
    {
        AddToParent(new FileSystemEntry(path, false, bytes, false));
        return this;
    }

    public FakeFileSystem Deny(string path)
    {
        _errors[path] = new UnauthorizedAccessException(path);
        return this;
    }

    public FakeFileSystem FailWithLongPath(string path)
    {
        _errors[path] = new PathTooLongException(path);
        return this;
    }

    public FakeFileSystem FailWithIoError(string path)
    {
        _errors[path] = new DirectoryNotFoundException(path);
        return this;
    }

    public IEnumerable<FileSystemEntry> EnumerateEntries(string path)
    {
        EnumeratedPaths.Add(path);
        if (_errors.TryGetValue(path, out var error))
        {
            throw error;
        }

        return _entries.TryGetValue(path, out var entries) ? entries : [];
    }

    private void AddToParent(FileSystemEntry entry)
    {
        var parent = Path.GetDirectoryName(entry.Path);
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        _entries.TryAdd(parent, []);
        _entries[parent].Add(entry);
    }
}
