using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Extensions;
using SnapRestore.Models;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public sealed class MemoryProcessingService(
    IOverlayService overlayService,
    IMemoriesHistoryService memoriesHistoryService,
    IExifToolService exifToolService) : IMemoryProcessingService
{
    public async Task<string?> ProcessAsync(
        SnapchatExportAnalysis analysis,
        string outputPath,
        IProgress<ProcessingProgress> progress,
        CancellationToken cancellationToken = default)
    {
        progress.Report(new ProcessingProgress());

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return null;
        }

        var outputFolder = CreateUniqueOutputFolder(outputPath);

        Directory.CreateDirectory(outputFolder);
        var reportFile = Path.Combine(outputFolder, "Report.txt");
        var hasMemoriesHistory = !string.IsNullOrWhiteSpace(analysis.MemoriesHistoryJsonPath);
        var modeDescription = hasMemoriesHistory
            ? "Mode: Restore media and apply metadata"
            : "Mode: Restore media only (metadata skipped; no memories JSON selected)";
        await File.WriteAllTextAsync(
            reportFile,
            $"SnapRestore Report\n\n{modeDescription}\n\n",
            cancellationToken);

        if (!analysis.IsValid)
        {
            await AppendProcessErrorAsync(reportFile, "Invalid Snapchat export.", cancellationToken);
            return outputFolder;
        }

        MemoryMatcher? memoryMatcher = null;
        if (hasMemoriesHistory)
        {
            var memories = await memoriesHistoryService.ParseAsync(
                analysis.MemoriesHistoryJsonPath!,
                cancellationToken);
            memoryMatcher = new MemoryMatcher(memories);
        }

        var files = analysis.MainMediaFiles
            .OrderBy(GetDateFromFileName)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalFiles = files.Count;

        progress.Report(new ProcessingProgress
        {
            TotalFiles = totalFiles,
            ProcessedFiles = 0,
            FailedFiles = 0
        });

        var failedFiles = 0;
        var processedFiles = 0;

        var dailyIndexes = new Dictionary<DateOnly, int>();

        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceFile = files[i];
                var fileFailed = false;

                try
                {
                    SnapchatMemoryHistoryItem? matchedMemory = null;
                    if (memoryMatcher is not null)
                    {
                        MediaMetadata metadata;
                        try
                        {
                            metadata = await exifToolService.ReadMetadataAsync(sourceFile, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            metadata = new MediaMetadata();
                            fileFailed = true;
                            await AppendFailureAsync(reportFile, sourceFile, ex, CancellationToken.None);
                        }

                        var mediaType = GetMediaType(sourceFile);
                        matchedMemory = memoryMatcher.Match(
                            mediaType,
                            metadata.CreateDateUtc,
                            metadata.FileModifyDateUtc);

                        if (matchedMemory is null)
                        {
                            await AppendNoMatchingMemoryAsync(reportFile, sourceFile, mediaType, cancellationToken);
                        }
                    }

                    var captureDateUtc = matchedMemory?.DateUtc;
                    var fileDate = captureDateUtc is not null
                        ? DateOnly.FromDateTime(captureDateUtc.Value)
                        : GetDateFromFileName(sourceFile);
                    var nextIndex = dailyIndexes.GetValueOrDefault(fileDate) + 1;
                    dailyIndexes[fileDate] = nextIndex;

                    var extension = Path.GetExtension(sourceFile).ToLowerInvariant();
                    var outputFileName = captureDateUtc is not null
                        ? $"{captureDateUtc:yyyy-MM-dd_HHmmss}_{nextIndex}{extension}"
                        : $"{fileDate:yyyy-MM-dd}_{nextIndex}{extension}";
                    var destinationFile = Path.Combine(outputFolder, outputFileName);

                    var overlaySucceeded = await overlayService.ApplyOverlayIfPresentAsync(
                        sourceFile,
                        destinationFile,
                        reportFile,
                        cancellationToken);
                    fileFailed |= !overlaySucceeded;

                    if (matchedMemory is not null)
                    {
                        await exifToolService.WriteMetadataAsync(
                            destinationFile,
                            matchedMemory.DateUtc,
                            matchedMemory.HasValidLocation ? matchedMemory.Latitude : null,
                            matchedMemory.HasValidLocation ? matchedMemory.Longitude : null,
                            cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    fileFailed = true;
                    await AppendFailureAsync(reportFile, sourceFile, ex, CancellationToken.None);
                }

                if (fileFailed)
                    failedFiles++;

                processedFiles = i + 1;

                progress.Report(new ProcessingProgress
                {
                    TotalFiles = totalFiles,
                    ProcessedFiles = processedFiles,
                    FailedFiles = failedFiles
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await File.AppendAllTextAsync(reportFile, "\nProcessing cancelled by user.\n", CancellationToken.None);
            throw;
        }
        finally
        {
            var successfulFiles = processedFiles - failedFiles;
            await File.AppendAllTextAsync(
                reportFile,
                $"\nSummary\nProcessed: {processedFiles}/{totalFiles}\nSuccess: {successfulFiles}\nFailed: {failedFiles}\n",
                CancellationToken.None);
        }

        return outputFolder;
    }

    private static DateOnly GetDateFromFileName(string path)
    {
        var fileName = Path.GetFileName(path);

        if (fileName.Length >= 10 &&
            DateOnly.TryParseExact(
                fileName[..10],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        return DateOnly.FromDateTime(File.GetLastWriteTime(path));
    }

    private static string GetMediaType(string path)
    {
        return path.IsVideoFile()
            ? "Video"
            : "Image";
    }

    private static string CreateUniqueOutputFolder(string outputPath)
    {
        var baseName = $"SnapRestore-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
        var candidate = Path.Combine(outputPath, baseName);
        var suffix = 1;

        while (Directory.Exists(candidate))
            candidate = Path.Combine(outputPath, $"{baseName}-{suffix++}");

        return candidate;
    }
    
    private static Task AppendFailureAsync(
        string reportFile,
        string sourceFile,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var originalFileName = Path.GetFileName(sourceFile);
        var message =
            $"Failed file: {originalFileName}\n" +
            $"Reason: {exception.Message}\n" +
            $"Source path: {sourceFile}\n\n";

        return File.AppendAllTextAsync(reportFile, message, cancellationToken);
    }

    private static Task AppendNoMatchingMemoryAsync(
        string reportFile,
        string sourceFile,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var originalFileName = Path.GetFileName(sourceFile);
        var message =
            $"No matching memory: {originalFileName}\n" +
            $"Media type: {mediaType}\n" +
            $"Source path: {sourceFile}\n\n";

        return File.AppendAllTextAsync(reportFile, message, cancellationToken);
    }

    private static Task AppendProcessErrorAsync(
        string reportFile,
        string reason,
        CancellationToken cancellationToken)
    {
        return File.AppendAllTextAsync(
            reportFile,
            $"Processing stopped\nReason: {reason}\n",
            cancellationToken);
    }

}
