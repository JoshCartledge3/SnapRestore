using System;
using System.IO;
using System.Linq;
using SnapRestore.Models;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public class SnapchatExportService : ISnapchatExportService
{
    private const string MemoriesFolderName = "memories";
    private const string JsonFolderName = "json";
    private const string MemoriesHistoryFileName = "memories_history.json";

    public SnapchatExportAnalysis Analyse(string memoriesDirectoryPath, string memoriesHistoryJsonPath)
    {
        var memoriesFound = Directory.Exists(memoriesDirectoryPath);
        var jsonFound = File.Exists(memoriesHistoryJsonPath)
                        && Path.GetExtension(memoriesHistoryJsonPath)
                            .Equals(".json", StringComparison.OrdinalIgnoreCase);

        var mainMediaFiles = memoriesFound
            ? Directory.EnumerateFiles(memoriesDirectoryPath)
                .Where(IsMainMediaFile)
                .ToList()
            : [];

        var isValid = memoriesFound && jsonFound && mainMediaFiles.Count > 0;

        return new SnapchatExportAnalysis
        {
            OriginalPath = memoriesDirectoryPath,
            ExportRootPath = Directory.GetParent(memoriesDirectoryPath)?.FullName,
            MemoriesDirectoryPath = memoriesFound ? memoriesDirectoryPath : null,
            MemoriesHistoryJsonPath = jsonFound ? memoriesHistoryJsonPath : null,
            IsValid = isValid,
            IsZip = false,
            JsonFound = jsonFound,
            MemoriesFolderFound = memoriesFound,
            MainMediaFiles = mainMediaFiles,
            MainMediaCount = mainMediaFiles.Count,
            StatusMessage = (memoriesFound, jsonFound, mainMediaFiles.Count) switch
            {
                (false, _, _) => "Memories folder missing",
                (_, false, _) => "JSON file missing",
                (_, _, 0) => "No media found",
                _ => "Ready"
            }
        };
    }

    public SnapchatExportAnalysis Analyse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Invalid(path, "Invalid file");
        }

        if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new SnapchatExportAnalysis
            {
                OriginalPath = path,
                IsValid = true,
                IsZip = true,
                JsonFound = false,
                MemoriesFolderFound = false,
                MainMediaCount = 0,
                StatusMessage = "ZIP ready"
            };
        }

        if (!Directory.Exists(path))
        {
            return Invalid(path, "Invalid file");
        }

        var exportRoot = FindExportRoot(path);
        if (exportRoot is null)
        {
            return new SnapchatExportAnalysis
            {
                OriginalPath = path,
                IsValid = false,
                StatusMessage = "Export structure not found"
            };
        }

        var memoriesDirectory = Path.Combine(exportRoot, MemoriesFolderName);
        var jsonFile = Path.Combine(exportRoot, JsonFolderName, MemoriesHistoryFileName);

        var memoriesFound = Directory.Exists(memoriesDirectory);
        var jsonFound = File.Exists(jsonFile);
        
        var mainMediaFiles = memoriesFound
            ? Directory.EnumerateFiles(memoriesDirectory)
                .Where(IsMainMediaFile)
                .ToList()
            : [];

        var mainMediaCount = mainMediaFiles.Count;
        
        var isValid = memoriesFound && jsonFound;

        return new SnapchatExportAnalysis
        {
            OriginalPath = path,
            ExportRootPath = exportRoot,
            MemoriesDirectoryPath = memoriesFound ? memoriesDirectory : null,
            MemoriesHistoryJsonPath = jsonFound ? jsonFile : null,
            IsValid = isValid,
            IsZip = false,
            JsonFound = jsonFound,
            MemoriesFolderFound = memoriesFound,
            MainMediaFiles =  mainMediaFiles,
            MainMediaCount = mainMediaCount,
            StatusMessage = isValid
                ? mainMediaCount > 0 ? "Folder ready" : "No media found"
                : "Export structure not found"
        };
    }

    private static SnapchatExportAnalysis Invalid(string? path, string statusMessage)
    {
        return new SnapchatExportAnalysis
        {
            OriginalPath = path ?? string.Empty,
            IsValid = false,
            StatusMessage = statusMessage
        };
    }

    private static string? FindExportRoot(string path)
    {
        if (IsExportRoot(path))
        {
            return path;
        }

        return Directory.EnumerateDirectories(path).FirstOrDefault(IsExportRoot);
    }

    private static bool IsExportRoot(string path)
    {
        return Directory.Exists(Path.Combine(path, MemoriesFolderName))
               || File.Exists(Path.Combine(path, JsonFolderName, MemoriesHistoryFileName));
    }

    private static bool IsMainMediaFile(string path)
    {
        var fileName = Path.GetFileName(path);

        return fileName.Contains("-main.", StringComparison.OrdinalIgnoreCase)
               && IsSupportedMediaFile(path);
    }

    private static bool IsSupportedMediaFile(string path)
    {
        var extension = Path.GetExtension(path);

        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }
}
