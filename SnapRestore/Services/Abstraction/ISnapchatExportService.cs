using SnapRestore.Models;

namespace SnapRestore.Services.Abstraction;

public interface ISnapchatExportService
{
    SnapchatExportAnalysis Analyse(string path);
    SnapchatExportAnalysis Analyse(string memoriesDirectoryPath, string memoriesHistoryJsonPath);
}
