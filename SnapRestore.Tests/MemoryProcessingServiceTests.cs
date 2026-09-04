using SnapRestore.Models;
using SnapRestore.Services;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Tests;

public sealed class MemoryProcessingServiceTests
{
    [Fact]
    public async Task ProcessAsync_CountsAtMostOneFailurePerSourceFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"snaprestore-test-{Guid.NewGuid():N}");
        var memoriesDirectory = Path.Combine(root, "memories");
        var outputDirectory = Path.Combine(root, "output");
        Directory.CreateDirectory(memoriesDirectory);
        Directory.CreateDirectory(outputDirectory);
        var source = Path.Combine(memoriesDirectory, "2026-01-02-id-main.jpg");
        await File.WriteAllTextAsync(source, "source");
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var progress = new ImmediateProgress();
        var service = new MemoryProcessingService(
            new FailingOverlayService(),
            new StubHistoryService(timestamp),
            new FailingWriteExifService(timestamp));

        var resultFolder = await service.ProcessAsync(
            new SnapchatExportAnalysis
            {
                OriginalPath = root,
                IsValid = true,
                MemoriesHistoryJsonPath = Path.Combine(root, "history.json"),
                MainMediaFiles = [source],
                MainMediaCount = 1
            },
            outputDirectory,
            progress);

        Assert.NotNull(resultFolder);
        Assert.Equal(1, progress.Last.ProcessedFiles);
        Assert.Equal(1, progress.Last.FailedFiles);
        Assert.Contains("Success: 0", await File.ReadAllTextAsync(Path.Combine(resultFolder!, "Report.txt")));
    }

    private sealed class ImmediateProgress : IProgress<ProcessingProgress>
    {
        public ProcessingProgress Last { get; private set; } = new();
        public void Report(ProcessingProgress value) => Last = value;
    }

    private sealed class FailingOverlayService : IOverlayService
    {
        public Task<bool> ApplyOverlayIfPresentAsync(
            string sourceFile,
            string destinationFile,
            string reportFile,
            CancellationToken cancellationToken = default)
        {
            File.Copy(sourceFile, destinationFile);
            return Task.FromResult(false);
        }
    }

    private sealed class StubHistoryService(DateTime timestamp) : IMemoriesHistoryService
    {
        public Task<IReadOnlyList<SnapchatMemoryHistoryItem>> ParseAsync(
            string memoriesHistoryJsonPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SnapchatMemoryHistoryItem>>(
                [new SnapchatMemoryHistoryItem { DateUtc = timestamp, MediaType = "Image" }]);
    }

    private sealed class FailingWriteExifService(DateTime timestamp) : IExifToolService
    {
        public Task<MediaMetadata> ReadMetadataAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaMetadata { CreateDateUtc = timestamp });

        public Task WriteMetadataAsync(
            string filePath,
            DateTime captureDateUtc,
            double? latitude,
            double? longitude,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated metadata failure");
    }
}
