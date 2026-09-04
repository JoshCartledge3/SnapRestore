using SnapRestore.Models;
using SnapRestore.Services;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Tests;

public sealed class OverlayServiceTests
{
    [Fact]
    public async Task ApplyOverlayIfPresentAsync_ReplacesPartialOutputWithOriginalOnFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"snaprestore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "2026-01-01-id-main.jpg");
        var overlay = Path.Combine(directory, "2026-01-01-id-overlay.png");
        var destination = Path.Combine(directory, "result.jpg");
        var report = Path.Combine(directory, "report.txt");
        await File.WriteAllTextAsync(source, "original");
        await File.WriteAllTextAsync(overlay, "not an image");
        await File.WriteAllTextAsync(destination, "partial");

        var service = new OverlayService(new StubToolResolver(), new UnusedProcessRunner());
        var success = await service.ApplyOverlayIfPresentAsync(source, destination, report);

        Assert.False(success);
        Assert.Equal("original", await File.ReadAllTextAsync(destination));
    }

    private sealed class StubToolResolver : IExternalToolResolver
    {
        public string GetExifToolPath() => "exiftool";
        public string GetFfmpegPath() => "ffmpeg";
    }

    private sealed class UnusedProcessRunner : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(
            string executable,
            IEnumerable<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The image test must not invoke FFmpeg.");
    }
}
