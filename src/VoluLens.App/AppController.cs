using System.IO;
using System.Text.Json;
using VoluLens.Core;

namespace VoluLens.App;

public interface IRecycleBinService
{
    Task MoveToRecycleBinAsync(string path, CancellationToken cancellationToken);
}

public interface IDriveProvider
{
    IReadOnlyList<DriveSummary> GetDrives();
}

public interface IPathLauncher
{
    void Open(string path);
}

public interface IGuidedCleanupLauncher
{
    void Open(GuidanceTarget target, string findingPath);
}

public sealed record ReportPayload(string SuggestedFileName, string Html);

public sealed record RecycleResultPayload(IReadOnlyList<string> FindingIds, long TotalBytes);

public sealed record AppResponse(string Type, string? Message = null, object? Data = null);

public sealed class AppController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IStorageScanner _scanner;
    private readonly IRecycleBinService _recycleBin;
    private readonly string _userRoot;
    private readonly IDriveProvider _driveProvider;
    private readonly IPathLauncher _pathLauncher;
    private readonly IGuidedCleanupLauncher _guidedCleanupLauncher;
    private readonly HtmlReportBuilder _reportBuilder;
    private readonly object _scanGate = new();
    private ScanResult? _currentResult;
    private PathSafety? _pathSafety;
    private CancellationTokenSource? _scanCancellation;

    public AppController(
        IStorageScanner scanner,
        IRecycleBinService recycleBin,
        string userRoot,
        IDriveProvider? driveProvider = null,
        IPathLauncher? pathLauncher = null,
        HtmlReportBuilder? reportBuilder = null,
        IGuidedCleanupLauncher? guidedCleanupLauncher = null)
    {
        _scanner = scanner;
        _recycleBin = recycleBin;
        _userRoot = userRoot;
        _driveProvider = driveProvider ?? new WindowsDriveProvider();
        _pathLauncher = pathLauncher ?? new ExplorerPathLauncher();
        _guidedCleanupLauncher = guidedCleanupLauncher ??
            new WindowsGuidedCleanupLauncher(_pathLauncher);
        _reportBuilder = reportBuilder ?? new HtmlReportBuilder();
    }

    public event Action<ScanProgress>? ScanProgressChanged;

    public async Task<AppResponse> HandleAsync(string json, CancellationToken cancellationToken = default)
    {
        AppCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<AppCommand>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return new("error", "Invalid command JSON.");
        }

        if (string.IsNullOrWhiteSpace(command?.Type))
        {
            return new("error", "Command type is required.");
        }

        try
        {
            return command.Type switch
            {
                "list-drives" => new("drives", Data: _driveProvider.GetDrives()),
                "start-scan" => await StartScanAsync(command, cancellationToken),
                "cancel-scan" => CancelScan(),
                "open-path" => OpenPath(command),
                "recycle" => await RecycleAsync(command, cancellationToken),
                "guided-cleanup" => GuidedCleanup(command),
                "export-report" => ExportReport(command),
                _ => new("error", "Unknown command.")
            };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new("error", exception.Message);
        }
    }

    private async Task<AppResponse> StartScanAsync(AppCommand command, CancellationToken cancellationToken)
    {
        if (command.Roots is not { Length: > 0 })
        {
            return new("error", "Select at least one drive or folder.");
        }

        CancellationTokenSource scanCancellation;
        lock (_scanGate)
        {
            if (_scanCancellation is not null)
            {
                return new("error", "A scan is already running.");
            }

            scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _scanCancellation = scanCancellation;
            _currentResult = null;
            _pathSafety = null;
        }

        try
        {
            var progress = new CallbackProgress<ScanProgress>(value => ScanProgressChanged?.Invoke(value));
            var result = await Task.Run(
                () => _scanner.ScanAsync(command.Roots, progress, scanCancellation.Token),
                CancellationToken.None);
            _currentResult = result;
            _pathSafety = new PathSafety(
                result.Roots,
                result.Findings
                    .Where(item => item.CleanupMode is
                        CleanupMode.Recycle or CleanupMode.ReviewRecycle)
                    .Select(item => item.Path));
            return new("scan-complete", Data: result);
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            return new("scan-cancelled", "Scan cancelled.");
        }
        finally
        {
            lock (_scanGate)
            {
                if (ReferenceEquals(_scanCancellation, scanCancellation))
                {
                    _scanCancellation = null;
                }
            }

            scanCancellation.Dispose();
        }
    }

    private AppResponse CancelScan()
    {
        lock (_scanGate)
        {
            if (_scanCancellation is null)
            {
                return new("error", "No scan is running.");
            }

            _scanCancellation.Cancel();
            return new("scan-cancel-requested");
        }
    }

    private AppResponse OpenPath(AppCommand command)
    {
        var finding = FindCurrent(command.FindingId);
        if (finding is null)
        {
            return new("error", "The finding is not part of the current scan.");
        }

        _pathLauncher.Open(finding.Path);
        return new("open-complete", Data: finding.Id);
    }

    private async Task<AppResponse> RecycleAsync(AppCommand command, CancellationToken cancellationToken)
    {
        if (_currentResult is null || command.FindingIds is not { Length: > 0 })
        {
            return new("error", "No current scan findings were selected.");
        }

        var requestedIds = command.FindingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var findings = _currentResult.Findings.Where(item => requestedIds.Contains(item.Id)).ToArray();
        if (findings.Length != requestedIds.Count)
        {
            return new("error", "One or more findings are not part of the current scan.");
        }

        if (_pathSafety is null || findings.Any(item =>
                !CanRecycle(item, command.AcknowledgedReviewIds) ||
                !_pathSafety.CanRecycle(item.Path)))
        {
            return new("error", "只有安全项和已确认风险的需判断项可以移入回收站。");
        }

        if (HasOverlappingPaths(findings) || HasUnselectedDescendant(findings))
        {
            return new("error", "Parent and child findings cannot be recycled together.");
        }

        var recycledIds = new List<string>();
        foreach (var finding in findings)
        {
            try
            {
                await _recycleBin.MoveToRecycleBinAsync(finding.Path, cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return recycledIds.Count == 0
                    ? new("error", exception.Message)
                    : new(
                        "recycle-partial",
                        exception.Message,
                        new RecycleResultPayload(recycledIds, _currentResult.TotalBytes));
            }

            RemoveCurrentFinding(finding);
            recycledIds.Add(finding.Id);
        }

        return new(
            "recycle-complete",
            Data: new RecycleResultPayload(recycledIds, _currentResult.TotalBytes));
    }

    private AppResponse GuidedCleanup(AppCommand command)
    {
        if (_currentResult is null || command.FindingIds is not { Length: > 0 })
        {
            return new("error", "没有选择可引导处理的项目。");
        }

        var requestedIds = command.FindingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var findings = _currentResult.Findings
            .Where(item => requestedIds.Contains(item.Id))
            .ToArray();
        if (findings.Length != requestedIds.Count || findings.Any(item =>
                item.Risk != RiskLevel.Protected ||
                item.CleanupMode != CleanupMode.Guided ||
                item.GuidanceTarget == GuidanceTarget.None))
        {
            return new("error", "只有当前扫描中的受保护项目可以使用引导处理。");
        }

        foreach (var finding in findings)
        {
            _guidedCleanupLauncher.Open(finding.GuidanceTarget, finding.Path);
        }

        return new("guidance-complete", Data: findings.Select(item => item.Id).ToArray());
    }

    private AppResponse ExportReport(AppCommand command)
    {
        if (_currentResult is null)
        {
            return new("error", "Run a scan before exporting a report.");
        }

        var payload = new ReportPayload(
            $"VoluLens-report-{DateTime.Now:yyyyMMdd-HHmmss}.html",
            _reportBuilder.Build(_currentResult, command.RedactUserPath ?? true, _userRoot));
        return new("report-ready", Data: payload);
    }

    private void RemoveCurrentFinding(ScanFinding finding)
    {
        if (_currentResult is null)
        {
            return;
        }

        var remaining = _currentResult.Findings
            .Where(item => !item.Id.Equals(finding.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _currentResult = _currentResult with
        {
            Findings = remaining,
            TotalBytes = Math.Max(0, _currentResult.TotalBytes - finding.Bytes)
        };
        _pathSafety = new PathSafety(
            _currentResult.Roots,
            remaining
                .Where(item => item.CleanupMode is
                    CleanupMode.Recycle or CleanupMode.ReviewRecycle)
                .Select(item => item.Path));
    }

    private static bool CanRecycle(
        ScanFinding finding,
        IReadOnlyList<string>? acknowledgedReviewIds)
    {
        if (finding.Risk == RiskLevel.Safe && finding.CleanupMode == CleanupMode.Recycle)
        {
            return true;
        }

        return finding.Risk == RiskLevel.Review &&
               finding.CleanupMode == CleanupMode.ReviewRecycle &&
               acknowledgedReviewIds?.Contains(
                   finding.Id,
                   StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool HasOverlappingPaths(IReadOnlyList<ScanFinding> findings)
    {
        var paths = findings
            .Select(item => Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path)))
            .ToArray();
        for (var index = 0; index < paths.Length; index++)
        {
            for (var other = index + 1; other < paths.Length; other++)
            {
                if (IsWithin(paths[index], paths[other]) || IsWithin(paths[other], paths[index]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasUnselectedDescendant(IReadOnlyList<ScanFinding> selectedFindings)
    {
        if (_currentResult is null)
        {
            return false;
        }

        var selectedIds = selectedFindings
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedPaths = selectedFindings
            .Select(item => Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path)))
            .ToArray();

        return _currentResult.Findings
            .Where(item => !selectedIds.Contains(item.Id))
            .Select(item => Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path)))
            .Any(otherPath => selectedPaths.Any(selectedPath =>
                IsWithin(otherPath, selectedPath)));
    }

    private static bool IsWithin(string candidate, string root) =>
        candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private ScanFinding? FindCurrent(string? findingId) =>
        string.IsNullOrWhiteSpace(findingId)
            ? null
            : _currentResult?.Findings.FirstOrDefault(item =>
                item.Id.Equals(findingId, StringComparison.OrdinalIgnoreCase));

    private sealed record AppCommand(
        string? Type,
        string[]? Roots,
        string[]? FindingIds,
        string? FindingId,
        bool? RedactUserPath,
        string[]? AcknowledgedReviewIds);

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
