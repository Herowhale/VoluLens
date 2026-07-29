namespace VoluLens.Core;

public sealed class SystemFileSystem : IFileSystem
{
    public IEnumerable<FileSystemEntry> EnumerateEntries(string path)
    {
        var directory = new DirectoryInfo(path);
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            var attributes = entry.Attributes;
            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            var isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
            var bytes = entry is FileInfo file ? file.Length : 0;
            yield return new(entry.FullName, isDirectory, bytes, isReparsePoint);
        }
    }
}
