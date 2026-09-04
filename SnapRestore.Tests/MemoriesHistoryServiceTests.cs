using System.Text.Json;
using SnapRestore.Services;

namespace SnapRestore.Tests;

public sealed class MemoriesHistoryServiceTests
{
    [Fact]
    public async Task ParseAsync_AcceptsZeroCoordinates()
    {
        var path = CreateJsonFile(new
        {
            Saved_Media = new[]
            {
                new Dictionary<string, string>
                {
                    ["Date"] = "2026-01-02 03:04:05 UTC",
                    ["Media Type"] = "Image",
                    ["Location"] = "Latitude, Longitude: 0, 0"
                }
            }
        }, replacePropertyName: true);

        var item = Assert.Single(await new MemoriesHistoryService().ParseAsync(path));

        Assert.True(item.HasValidLocation);
        Assert.Equal(0, item.Latitude);
        Assert.Equal(0, item.Longitude);
    }

    [Fact]
    public async Task ParseAsync_RejectsUnknownRootArray()
    {
        var path = CreateJsonFile(new { Other = Array.Empty<object>() });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new MemoriesHistoryService().ParseAsync(path));
    }

    private static string CreateJsonFile(object value, bool replacePropertyName = false)
    {
        var json = JsonSerializer.Serialize(value);
        if (replacePropertyName)
            json = json.Replace("Saved_Media", "Saved Media", StringComparison.Ordinal);

        var path = Path.Combine(Path.GetTempPath(), $"snaprestore-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
