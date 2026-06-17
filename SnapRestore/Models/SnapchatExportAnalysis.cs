using System.Collections.Generic;

namespace SnapRestore.Models;

public sealed class SnapchatExportAnalysis
{
    public required string OriginalPath { get; init; }
    public string? ExportRootPath { get; init; }
    public string? MemoriesDirectoryPath { get; init; }
    public string? MemoriesHistoryJsonPath { get; init; }
    public bool IsValid { get; init; }
    public bool IsZip { get; init; }
    public bool JsonFound { get; init; }
    public bool MemoriesFolderFound { get; init; }
    public int MainMediaCount { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> MainMediaFiles { get; init; } = [];
}
