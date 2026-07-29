using System.IO;

namespace VoluLens.App;

internal static class WebViewDataPath
{
    public static string Prepare(string baseDirectory, string localApplicationData)
    {
        var portable = Resolve(baseDirectory, localApplicationData, portableWritable: true);
        if (TryPrepare(portable))
        {
            return portable;
        }

        var fallback = Resolve(baseDirectory, localApplicationData, portableWritable: false);
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    internal static string Resolve(
        string baseDirectory,
        string localApplicationData,
        bool portableWritable) =>
        portableWritable
            ? Path.Combine(baseDirectory, "WebView2Data")
            : Path.Combine(localApplicationData, "VoluLens", "WebView2Data");

    private static bool TryPrepare(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe, bufferSize: 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
