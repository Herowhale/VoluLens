namespace VoluLens.Core;

public sealed class StorageClassifier
{
    private readonly string? _userRoot;

    public StorageClassifier(string? userRoot = null)
    {
        _userRoot = string.IsNullOrWhiteSpace(userRoot) ? null : Normalize(userRoot);
    }

    public Classification Classify(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = Normalize(path);
        var userRoot = _userRoot ?? InferUserRoot(normalized);

        if (IsWindowsPath(normalized))
        {
            return new(
                RiskLevel.Protected,
                "Windows 系统",
                "此位置由 Windows 管理，请使用系统存储设置或磁盘清理处理，不要手动删除。",
                CleanupMode.Guided,
                GuidanceTarget.StorageSettings);
        }

        if (IsInstalledApplication(normalized))
        {
            return new(
                RiskLevel.Protected,
                "已安装应用",
                "这里包含已安装应用的程序文件，请通过 Windows 应用卸载入口处理。",
                CleanupMode.Guided,
                GuidanceTarget.AppsSettings);
        }

        if (ContainsSegment(normalized, "downloads"))
        {
            return new(
                RiskLevel.Review,
                "下载内容",
                "下载目录可能同时包含安装包和个人文件，清理前需要逐项核对。",
                CleanupMode.ReviewRecycle);
        }

        if (IsUserDocuments(normalized))
        {
            return new(
                RiskLevel.Review,
                "用户文档",
                "这里可能包含文档、项目、聊天文件或设计稿，清理前需要确认内容和备份。",
                CleanupMode.ReviewRecycle);
        }

        if (IsMediaContent(normalized))
        {
            return new(
                RiskLevel.Review,
                "媒体内容",
                "这里包含图片、视频或音乐等用户内容，删除前请确认是否仍需保留或已经备份。",
                CleanupMode.ReviewRecycle);
        }

        if (IsKnownTemporary(normalized, userRoot))
        {
            return new(
                RiskLevel.Safe,
                "临时文件",
                "应用或系统运行时产生的临时内容通常可以在需要时重新生成。",
                CleanupMode.Recycle);
        }

        if (IsKnownCache(normalized, userRoot))
        {
            return new(
                RiskLevel.Safe,
                "应用缓存",
                "这是已知的可重新生成缓存位置，不包含账号、书签或个人文件。",
                CleanupMode.Recycle);
        }

        if (IsKnownDevelopmentCache(normalized, userRoot))
        {
            return new(
                RiskLevel.Safe,
                "开发缓存",
                "这是构建工具或依赖管理器生成的缓存，下次使用时可以重新下载或生成。",
                CleanupMode.Recycle);
        }

        if (IsApplicationData(normalized, userRoot))
        {
            return new(
                RiskLevel.Review,
                "应用数据",
                "这里可能包含应用配置、登录状态、离线内容或业务数据，清理前需要确认影响。",
                CleanupMode.ReviewRecycle);
        }

        return new(
            RiskLevel.Review,
            "其他待识别",
            "VoluLens 无法确认此位置的内容可以重新生成，请打开目录核对后再决定。",
            CleanupMode.ReviewRecycle);
    }

    private static string Normalize(string path) =>
        path.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();

    private static bool IsWindowsPath(string path) =>
        path.StartsWith(@"c:\windows", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(@"\windows\", StringComparison.OrdinalIgnoreCase);

    private static bool IsInstalledApplication(string path) =>
        ContainsSegment(path, "program files") ||
        ContainsSegment(path, "program files (x86)");

    private static bool IsKnownTemporary(string path, string? userRoot) =>
        userRoot is not null &&
        IsWithin(path, userRoot + @"\appdata\local\temp");

    private static bool IsKnownCache(string path, string? userRoot)
    {
        if (userRoot is null)
        {
            return false;
        }

        var localAppData = userRoot + @"\appdata\local";
        return IsWithin(path, localAppData) &&
               (ContainsSegment(path, "cache") || ContainsSegment(path, "caches")) ||
               IsWithin(path, userRoot + @"\.cache");
    }

    private static bool IsKnownDevelopmentCache(string path, string? userRoot)
    {
        if (userRoot is null)
        {
            return false;
        }

        return IsWithin(path, userRoot + @"\.npm") ||
               IsWithin(path, userRoot + @"\.nuget\packages") ||
               IsWithin(path, userRoot + @"\.gradle\caches") ||
               IsWithin(path, userRoot + @"\.m2\repository") ||
               IsWithin(path, userRoot + @"\appdata\local\pip\cache") ||
               IsWithin(path, userRoot + @"\appdata\local\yarn\cache");
    }

    private static bool IsApplicationData(string path, string? userRoot) =>
        userRoot is not null &&
        (IsWithin(path, userRoot + @"\appdata\local") ||
         IsWithin(path, userRoot + @"\appdata\roaming"));

    private static bool IsUserDocuments(string path) =>
        ContainsSegment(path, "documents") ||
        ContainsSegment(path, "desktop") ||
        path.Contains(@"\wechat files", StringComparison.OrdinalIgnoreCase);

    private static bool IsMediaContent(string path) =>
        ContainsSegment(path, "pictures") ||
        ContainsSegment(path, "videos") ||
        ContainsSegment(path, "music");

    private static bool ContainsSegment(string path, string segment)
    {
        var wrapped = $"\\{path.Trim('\\')}\\";
        return wrapped.Contains($"\\{segment}\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string? InferUserRoot(string path)
    {
        const string marker = @"\users\";
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var userNameStart = markerIndex + marker.Length;
        var separator = path.IndexOf('\\', userNameStart);
        return separator < 0 ? path : path[..separator];
    }

    private static bool IsWithin(string candidate, string root) =>
        candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
}
