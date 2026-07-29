using System.IO;
using System.Reflection;

namespace VoluLens.App;

public static class EmbeddedAssets
{
    public static string ReadIndexHtml()
    {
        var html = ReadText("VoluLens.App.Assets.index.html");
        var lucide = ReadText("VoluLens.App.Assets.lucide.min.js");
        return html.Replace("/*__LUCIDE_LIBRARY__*/", lucide, StringComparison.Ordinal);
    }

    private static string ReadText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
