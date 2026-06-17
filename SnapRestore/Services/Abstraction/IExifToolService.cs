using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;

namespace SnapRestore.Services.Abstraction;

public interface IExifToolService
{
    Task<MediaMetadata> ReadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default);
    
    Task WriteGpsAsync(
        string filePath,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
