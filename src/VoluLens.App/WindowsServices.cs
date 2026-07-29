using System.Diagnostics;
using System.IO;
using VoluLens.Core;

namespace VoluLens.App;

public sealed class WindowsDriveProvider : IDriveProvider
{
    public IReadOnlyList<DriveSummary> GetDrives()
    {
        var drives = new List<DriveSummary>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.Name
                    : drive.VolumeLabel;
                drives.Add(new(
                    label,
                    drive.RootDirectory.FullName,
                    drive.TotalSize,
                    drive.TotalSize - drive.AvailableFreeSpace,
                    drive.AvailableFreeSpace));
            }
            catch (IOException)
            {
                // A removable drive can disappear while it is being queried.
            }
            catch (UnauthorizedAccessException)
            {
                // Omit drives whose metadata cannot be read.
            }
        }

        return drives;
    }
}

public sealed class ExplorerPathLauncher : IPathLauncher
{
    public void Open(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        throw new FileNotFoundException("The selected finding no longer exists.", path);
    }
}

public sealed class WindowsGuidedCleanupLauncher(IPathLauncher pathLauncher)
    : IGuidedCleanupLauncher
{
    public void Open(GuidanceTarget target, string findingPath)
    {
        switch (target)
        {
            case GuidanceTarget.AppsSettings:
                OpenSettings("ms-settings:appsfeatures");
                break;
            case GuidanceTarget.StorageSettings:
                OpenSettings("ms-settings:storagesense");
                break;
            case GuidanceTarget.Explorer:
                pathLauncher.Open(findingPath);
                break;
            default:
                throw new InvalidOperationException("Unsupported guided cleanup target.");
        }
    }

    private static void OpenSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException("无法打开 Windows 处理入口。", exception);
        }
    }
}
