using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;

namespace SnapRestore.Services.Abstraction;

public interface IMemoriesHistoryService
{
    Task<IReadOnlyList<SnapchatMemoryHistoryItem>> ParseAsync(
        string memoriesHistoryJsonPath,
        CancellationToken cancellationToken = default);
}
