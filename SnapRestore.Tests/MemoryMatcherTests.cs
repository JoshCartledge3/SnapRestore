using SnapRestore.Models;
using SnapRestore.Services;

namespace SnapRestore.Tests;

public sealed class MemoryMatcherTests
{
    [Fact]
    public void Match_UsesClosestItemOnlyOnce()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var first = new SnapchatMemoryHistoryItem { DateUtc = timestamp, MediaType = "Image" };
        var second = new SnapchatMemoryHistoryItem { DateUtc = timestamp.AddSeconds(10), MediaType = "Image" };
        var matcher = new MemoryMatcher([first, second]);

        Assert.Same(first, matcher.Match("image", timestamp.AddSeconds(1)));
        Assert.Same(second, matcher.Match("Image", timestamp.AddSeconds(1)));
        Assert.Null(matcher.Match("Image", timestamp.AddSeconds(1)));
    }
}
