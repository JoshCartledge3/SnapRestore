using SnapRestore.Models;

namespace SnapRestore.Services.Abstraction;

public interface ISnapchatExportService
{
    SnapchatExportAnalysis Analyse(string path);
}