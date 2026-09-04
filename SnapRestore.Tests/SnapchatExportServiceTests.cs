using SnapRestore.Services;

namespace SnapRestore.Tests;

public sealed class SnapchatExportServiceTests
{
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
