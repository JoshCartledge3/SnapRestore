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

public class MemoryProcessingService(
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

        var outputFolder = Path.Combine(
            outputPath,
            $"SnapRestore-{DateTime.Now:yyyyMMdd-HHmmss}");

        Directory.CreateDirectory(outputFolder);
        var reportFile = Path.Combine(outputFolder, "Report.txt");
        await File.WriteAllTextAsync(reportFile, "SnapRestore Report\n\n", cancellationToken);

        if (!analysis.IsValid)
        {
            await AppendProcessErrorAsync(reportFile, "Invalid Snapchat export.", cancellationToken);
            return outputFolder;
        }

        if (string.IsNullOrWhiteSpace(analysis.MemoriesHistoryJsonPath))
        {
            await AppendProcessErrorAsync(reportFile, "Memories history JSON file is required.", cancellationToken);
            return outputFolder;
        }

        var memories = await memoriesHistoryService.ParseAsync(analysis.MemoriesHistoryJsonPath, cancellationToken);

        var files = analysis.MainMediaFiles
            .OrderBy(GetDateFromFileName)
            .ThenBy(Path.GetFileName)
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

        for (var i = 0; i < files.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var sourceFile = files[i];

            try
            {
                var metadata = await exifToolService.ReadMetadataAsync(sourceFile, CancellationToken.None);
                var mediaType = GetMediaType(sourceFile);

                var matchedMemory =
                    FindMatchingMemory(memories, mediaType, metadata.CreateDateUtc)
                    ?? FindMatchingMemory(memories, mediaType, metadata.FileModifyDateUtc);

                if (matchedMemory is null)
                {
                    await AppendNoMatchingMemoryAsync(reportFile, sourceFile, mediaType, CancellationToken.None);
                }

                var fileDate = GetDateFromFileName(sourceFile);
                var nextIndex = dailyIndexes.GetValueOrDefault(fileDate) + 1;
                dailyIndexes[fileDate] = nextIndex;

                var extension = Path.GetExtension(sourceFile).ToLowerInvariant();

                var outputFileName = $"{fileDate:yyyy-MM-dd}_{nextIndex}{extension}";
                var destinationFile = Path.Combine(outputFolder, outputFileName);

                var success = await overlayService.ApplyOverlayIfPresentAsync(
                    sourceFile,
                    destinationFile,
                    reportFile,
                    CancellationToken.None);

                if (!success)
                {
                    failedFiles++;
                }
                
                if (matchedMemory?.HasValidLocation == true)
                {
                    await exifToolService.WriteGpsAsync(
                        destinationFile,
                        matchedMemory.Latitude!.Value,
                        matchedMemory.Longitude!.Value,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                failedFiles++;
                await AppendFailureAsync(reportFile, sourceFile, ex, CancellationToken.None);
            }

            processedFiles = i + 1;

            progress.Report(new ProcessingProgress
            {
                TotalFiles = totalFiles,
                ProcessedFiles = processedFiles,
                FailedFiles = failedFiles
            });

            await Task.Yield();
        }

        var successfulFiles = processedFiles - failedFiles;
        await File.AppendAllTextAsync(
            reportFile,
            $"\nSummary\nSuccess: {successfulFiles}\nFailed: {failedFiles}\n",
            CancellationToken.None);

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

    private static SnapchatMemoryHistoryItem? FindMatchingMemory(
        IReadOnlyList<SnapchatMemoryHistoryItem> memories,
        string mediaType,
        DateTime? fileDateUtc)
    {
        if (fileDateUtc is null)
            return null;

        return memories
            .Where(x => x.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase))
            .Select(x => new
            {
                Memory = x,
                Difference = Math.Abs((x.DateUtc - fileDateUtc.Value).TotalSeconds)
            })
            .Where(x => x.Difference <= 60)
            .OrderBy(x => x.Difference)
            .Select(x => x.Memory)
            .FirstOrDefault();
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
