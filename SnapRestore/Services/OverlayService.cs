using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SnapRestore.Extensions;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public class OverlayService : IOverlayService
{
    public OverlayService(IExternalToolResolver externalToolResolver)
    {
        var ffmpegDirectory = externalToolResolver.GetFfmpegDirectory();

        if (!string.IsNullOrWhiteSpace(ffmpegDirectory))
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegDirectory);
        }
    }

    public async Task<bool> ApplyOverlayIfPresentAsync(
        string sourceFile,
        string destinationFile,
        string reportFile,
        CancellationToken cancellationToken = default)
    {
        var overlayFile = FindOverlayFile(sourceFile);

        if (overlayFile is null)
        {
            File.Copy(sourceFile, destinationFile, overwrite: false);
            return true;
        }

        if (sourceFile.IsImageFile())
        {
            try
            {
                await FlattenImageOverlayAsync(sourceFile, overlayFile, destinationFile, cancellationToken);
            }
            catch (Exception ex)
            {
                File.Copy(sourceFile, destinationFile, overwrite: false);
                await AppendOverlayWarningAsync(reportFile, sourceFile, overlayFile, ex.Message, cancellationToken);
                return false;
            }

            return true;
        }

        if (sourceFile.IsVideoFile())
        {
            var failureReason = await BurnVideoOverlayAsync(sourceFile, overlayFile, destinationFile);
            if (failureReason is not null)
            {
                File.Copy(sourceFile, destinationFile, overwrite: false);
                await AppendOverlayWarningAsync(reportFile, sourceFile, overlayFile, failureReason, cancellationToken);
                return false;
            }

            return true;
        }

        File.Copy(sourceFile, destinationFile, overwrite: false);
        return true;
    }

    private static string? FindOverlayFile(string mainFile)
    {
        var directory = Path.GetDirectoryName(mainFile);
        if (directory is null)
            return null;

        var mainFileNameWithoutExtension = Path.GetFileNameWithoutExtension(mainFile);

        var overlayFileNameWithoutExtension = mainFileNameWithoutExtension.Replace(
            "-main",
            "-overlay",
            StringComparison.OrdinalIgnoreCase);

        var overlayPath = Path.Combine(directory, $"{overlayFileNameWithoutExtension}.png");

        return File.Exists(overlayPath)
            ? overlayPath
            : null;
    }

    private static async Task FlattenImageOverlayAsync(
        string mainFile,
        string overlayFile,
        string destinationFile,
        CancellationToken cancellationToken)
    {
        using var mainImage =
            await Image.LoadAsync<Rgba32>(mainFile, cancellationToken);

        using var originalOverlay =
            await Image.LoadAsync<Rgba32>(overlayFile, cancellationToken);

        var targetSize = mainImage.Size;
        using var overlayImage = originalOverlay.Size == mainImage.Size
            ? originalOverlay.Clone()
            : originalOverlay.Clone(context => context.Resize(targetSize));

        mainImage.ProcessPixelRows(overlayImage, static (mainAccessor, overlayAccessor) =>
        {
            for (var y = 0; y < mainAccessor.Height; y++)
            {
                var mainRow = mainAccessor.GetRowSpan(y);
                var overlayRow = overlayAccessor.GetRowSpan(y);

                for (var x = 0; x < mainRow.Length; x++)
                {
                    mainRow[x] = AlphaBlend(overlayRow[x], mainRow[x]);
                }
            }
        });

        await mainImage.SaveAsync(destinationFile, cancellationToken);
    }

    private static async Task<string?> BurnVideoOverlayAsync(
        string videoFile,
        string overlayFile,
        string destinationFile)
    {
        try
        {
            var success = await FFMpegArguments
                .FromFileInput(videoFile)
                .AddFileInput(overlayFile)
                .OutputToFile(
                    destinationFile,
                    overwrite: true,
                    options => options
                        .WithCustomArgument("-filter_complex \"[0:v][1:v]overlay=0:0:format=auto\"")
                        .WithVideoCodec("libx264")
                        .WithCustomArgument("-crf 18")
                        .WithCustomArgument("-preset veryfast")
                        .WithCustomArgument("-c:a copy")
                        .WithFastStart())
                .ProcessAsynchronously();

            return success
                ? null
                : $"Failed to burn overlay onto video '{Path.GetFileName(videoFile)}'.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static Task AppendOverlayWarningAsync(
        string reportFile,
        string sourceFile,
        string overlayFile,
        string reason,
        CancellationToken cancellationToken)
    {
        var originalFileName = Path.GetFileName(sourceFile);
        var overlayFileName = Path.GetFileName(overlayFile);
        var message =
            $"Overlay issue: {originalFileName}\n" +
            $"Overlay file: {overlayFileName}\n" +
            $"Reason: {reason}\n" +
            "Result: Main file copied without overlay.\n\n";

        return File.AppendAllTextAsync(reportFile, message, cancellationToken);
    }

    private static Rgba32 AlphaBlend(Rgba32 source, Rgba32 destination)
    {
        if (source.A == 0)
            return destination;

        if (source.A == byte.MaxValue)
            return source;

        var alpha = source.A / 255f;
        var inverseAlpha = 1f - alpha;

        return new Rgba32(
            (byte)Math.Clamp(source.R * alpha + destination.R * inverseAlpha, 0, 255),
            (byte)Math.Clamp(source.G * alpha + destination.G * inverseAlpha, 0, 255),
            (byte)Math.Clamp(source.B * alpha + destination.B * inverseAlpha, 0, 255),
            byte.MaxValue);
    }
}
