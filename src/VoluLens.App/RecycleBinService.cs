using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace VoluLens.App;

public interface IRecycleOperation
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    void RecycleDirectory(string path);
    void RecycleFile(string path);
}

public sealed class RecycleBinService(IRecycleOperation? operation = null) : IRecycleBinService
{
    private readonly IRecycleOperation _operation = operation ?? new WindowsRecycleOperation();

    public Task MoveToRecycleBinAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (_operation.DirectoryExists(path))
        {
            _operation.RecycleDirectory(path);
            return Task.CompletedTask;
        }

        if (_operation.FileExists(path))
        {
            _operation.RecycleFile(path);
            return Task.CompletedTask;
        }

        throw new FileNotFoundException("The selected finding no longer exists.", path);
    }
}

internal sealed class WindowsRecycleOperation : IRecycleOperation
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public void RecycleDirectory(string path) => FileSystem.DeleteDirectory(
        path,
        UIOption.OnlyErrorDialogs,
        RecycleOption.SendToRecycleBin,
        UICancelOption.ThrowException);

    public void RecycleFile(string path) => FileSystem.DeleteFile(
        path,
        UIOption.OnlyErrorDialogs,
        RecycleOption.SendToRecycleBin,
        UICancelOption.ThrowException);
}
