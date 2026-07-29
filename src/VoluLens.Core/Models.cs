namespace VoluLens.Core;

public enum RiskLevel
{
    Safe,
    Review,
    Protected
}

public enum CleanupMode
{
    Recycle,
    ReviewRecycle,
    Guided
}

public enum GuidanceTarget
{
    None,
    AppsSettings,
    StorageSettings,
    Explorer
}

public sealed record Classification(
    RiskLevel Risk,
    string Category,
    string Reason,
    CleanupMode CleanupMode = CleanupMode.ReviewRecycle,
    GuidanceTarget GuidanceTarget = GuidanceTarget.None);

public sealed record ScanFinding(
    string Id,
    string Path,
    string Name,
    long Bytes,
    RiskLevel Risk,
    string Category,
    string Reason,
    CleanupMode CleanupMode = CleanupMode.ReviewRecycle,
    GuidanceTarget GuidanceTarget = GuidanceTarget.None);

public sealed record ScanProgress(
    string CurrentPath,
    long FilesScanned,
    long FoldersScanned,
    long BytesScanned);

public sealed record FindingGroup(
    string Key,
    RiskLevel Risk,
    string Category,
    string Reason,
    CleanupMode CleanupMode,
    long TotalBytes,
    IReadOnlyList<ScanFinding> Findings);

public sealed record ScanResult(
    IReadOnlyList<string> Roots,
    IReadOnlyList<ScanFinding> Findings,
    IReadOnlyList<string> DeniedPaths,
    long TotalBytes,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public IReadOnlyList<FindingGroup> Groups => FindingGrouper.Group(Findings);
}

public sealed record DriveSummary(
    string Name,
    string RootPath,
    long TotalBytes,
    long UsedBytes,
    long FreeBytes);
