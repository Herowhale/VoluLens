using VoluLens.Core;

namespace VoluLens.Core.Tests;

public sealed class PathSafetyTests
{
    [Theory]
    [InlineData(@"C:\Windows", false)]
    [InlineData(@"C:\Users\A\AppData\Local\Temp\cache", true)]
    [InlineData(@"C:\Users\A\AppData\Local\Temp\cache\nested", true)]
    [InlineData(@"C:\Users\A\AppData\Local\Temp\..\..\..\Windows", false)]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\cache", false)]
    public void CanRecycle_ValidatesCanonicalAllowlist(string path, bool expected)
    {
        var safety = new PathSafety(
            [@"C:\Users\A"],
            [@"C:\Users\A\AppData\Local\Temp\cache"]);

        Assert.Equal(expected, safety.CanRecycle(path));
    }

    [Theory]
    [InlineData(@"D:\Projects\old-build", true)]
    [InlineData(@"D:\Projects\old-build\nested", true)]
    [InlineData(@"D:\Program Files\Example", false)]
    [InlineData(@"C:\Users\A\Downloads", false)]
    public void CanRecycle_AllowsOnlyScannedAndAllowlistedPaths(
        string path,
        bool expected)
    {
        var safety = new PathSafety(
            [@"D:\"],
            [@"D:\Projects\old-build", @"D:\Program Files\Example"]);

        Assert.Equal(expected, safety.CanRecycle(path));
    }
}
