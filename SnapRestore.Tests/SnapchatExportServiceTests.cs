using SnapRestore.Services;

namespace SnapRestore.Tests;

public sealed class SnapchatExportServiceTests
{
    [Fact]
    public void Analyse_AcceptsMemoriesFolderWithoutJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"snaprestore-test-{Guid.NewGuid():N}");
        var memoriesDirectory = Path.Combine(root, "memories");
        Directory.CreateDirectory(memoriesDirectory);
        File.WriteAllText(Path.Combine(memoriesDirectory, "2026-01-02-id-main.jpg"), "source");

        try
        {
            var result = new SnapchatExportService().Analyse(memoriesDirectory, string.Empty);

            Assert.True(result.IsValid);
            Assert.False(result.JsonFound);
            Assert.Null(result.MemoriesHistoryJsonPath);
            Assert.Equal(1, result.MainMediaCount);
            Assert.Contains("metadata will be skipped", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyse_DoesNotClaimZipIsProcessable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"snaprestore-test-{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(path, []);

        var result = new SnapchatExportService().Analyse(path);

        Assert.False(result.IsValid);
        Assert.True(result.IsZip);
        Assert.Contains("extract", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
