namespace VoluLens.Core;

public static class FindingGrouper
{
    public static IReadOnlyList<FindingGroup> Group(IEnumerable<ScanFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings
            .GroupBy(item => new { item.Risk, item.Category })
            .Select(group =>
            {
                var orderedFindings = group
                    .OrderByDescending(item => item.Bytes)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var first = orderedFindings[0];
                return new FindingGroup(
                    $"{(int)group.Key.Risk}:{group.Key.Category}",
                    group.Key.Risk,
                    group.Key.Category,
                    first.Reason,
                    first.CleanupMode,
                    orderedFindings.Sum(item => item.Bytes),
                    orderedFindings);
            })
            .OrderBy(group => group.Risk)
            .ThenByDescending(group => group.TotalBytes)
            .ThenBy(group => group.Category, StringComparer.Ordinal)
            .ToArray();
    }
}
