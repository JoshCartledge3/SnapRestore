using System;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;

namespace SnapRestore.Services.Abstraction;

public interface IMemoryProcessingService
{
    Task<string?> ProcessAsync(
        SnapchatExportAnalysis analysis,
        string outputPath,
        IProgress<ProcessingProgress> progress,
        CancellationToken cancellationToken = default);
}