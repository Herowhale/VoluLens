namespace VoluLens.Core;

public sealed class PathSafety
{
    private readonly string[] _scanRoots;
    private readonly string[] _allowedRoots;
    private readonly string[] _protectedRoots;

    public PathSafety(IEnumerable<string> scanRoots, IEnumerable<string> allowedRoots)
    {
        ArgumentNullException.ThrowIfNull(scanRoots);
        ArgumentNullException.ThrowIfNull(allowedRoots);

        _scanRoots = scanRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_scanRoots.Length == 0)
        {
            throw new ArgumentException("At least one scan root is required.", nameof(scanRoots));
        }

        _allowedRoots = allowedRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _protectedRoots = _scanRoots
            .Select(Path.GetPathRoot)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(root => new[]
            {
                Path.Combine(root!, "Windows"),
                Path.Combine(root!, "Program Files"),
                Path.Combine(root!, "Program Files (x86)"),
                Path.Combine(root!, "ProgramData")
            })
            .Select(Canonicalize)
            .ToArray();
    }

    public bool CanRecycle(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string canonical;
        try
        {
            canonical = Canonicalize(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!_scanRoots.Any(root => IsWithin(canonical, root)) ||
            _protectedRoots.Any(root => IsWithin(canonical, root)))
        {
            return false;
        }

        return _allowedRoots.Any(root => IsWithin(canonical, root));
    }

    private static string Canonicalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsWithin(string candidate, string root)
    {
        if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
