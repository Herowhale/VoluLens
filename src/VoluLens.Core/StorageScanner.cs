using System.Security.Cryptography;
using System.Text;

namespace VoluLens.Core;

public sealed class StorageScanner(IFileSystem fileSystem, StorageClassifier classifier) : IStorageScanner
{
    public async Task<ScanResult> ScanAsync(
        IReadOnlyList<string> roots,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var startedAt = DateTimeOffset.UtcNow;
        var queue = new Queue<(string Directory, string? Bucket)>();
        var denied = new List<string>();
        var bucketSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var incompleteBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mixedRiskBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        long filesScanned = 0;
        long foldersScanned = 0;

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var rootClassification = classifier.Classify(root);
            queue.Enqueue((root, rootClassification.Risk == RiskLevel.Safe ? root : null));
        }

        await Task.Yield();
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, bucket) = queue.Dequeue();
            foldersScanned++;

            try
            {
                foreach (var entry in fileSystem.EnumerateEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.IsReparsePoint)
                    {
                        if (bucket is not null)
                        {
                            incompleteBuckets.Add(bucket);
                        }

                        continue;
                    }

                    if (entry.IsDirectory)
                    {
                        var classification = classifier.Classify(entry.Path);
                        if (bucket is not null &&
                            classifier.Classify(bucket).Risk == RiskLevel.Safe &&
                            classification.Risk != RiskLevel.Safe)
                        {
                            mixedRiskBuckets.Add(bucket);
                        }

                        var directoryBucket = bucket is not null &&
                                              classifier.Classify(bucket).Risk == RiskLevel.Safe
                            ? bucket
                            : classification.Category == "其他待识别"
                                ? bucket ?? entry.Path
                                : entry.Path;
                        queue.Enqueue((entry.Path, directoryBucket));
                        continue;
                    }

                    var entryBucket = bucket ?? directory;
                    bucketSizes[entryBucket] = bucketSizes.GetValueOrDefault(entryBucket) + entry.Bytes;
                    totalBytes += entry.Bytes;
                    filesScanned++;
                    progress?.Report(new(entry.Path, filesScanned, foldersScanned, totalBytes));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                denied.Add(directory);
                if (bucket is not null)
                {
                    incompleteBuckets.Add(bucket);
                }
            }
        }

        var findings = bucketSizes
            .OrderByDescending(pair => pair.Value)
            .Select(pair => CreateFinding(
                pair.Key,
                pair.Value,
                incompleteBuckets.Contains(pair.Key),
                mixedRiskBuckets.Contains(pair.Key)))
            .ToArray();

        return new(
            roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            findings,
            denied,
            totalBytes,
            startedAt,
            DateTimeOffset.UtcNow);
    }

    private ScanFinding CreateFinding(
        string path,
        long bytes,
        bool incomplete,
        bool mixedRiskContent)
    {
        var classification = classifier.Classify(path);
        if (incomplete && classification.Risk == RiskLevel.Safe)
        {
            classification = new(
                RiskLevel.Review,
                "扫描不完整",
                "此目录有部分内容无法读取，因此不能按安全项目自动处理。",
                CleanupMode.ReviewRecycle);
        }
        else if (mixedRiskContent && classification.Risk == RiskLevel.Safe)
        {
            classification = new(
                RiskLevel.Review,
                "包含不同风险内容",
                "此目录同时包含需要人工判断的内容，清理整个目录前必须先核对。",
                CleanupMode.ReviewRecycle);
        }

        return new(
            CreateId(path),
            path,
            GetName(path),
            bytes,
            classification.Risk,
            classification.Category,
            classification.Reason,
            classification.CleanupMode,
            classification.GuidanceTarget);
    }

    private static string CreateId(string path)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant()));
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static string GetName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed) is { Length: > 0 } name ? name : path;
    }
}
