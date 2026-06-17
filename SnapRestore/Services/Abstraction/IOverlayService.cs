using System.Threading;
using System.Threading.Tasks;

namespace SnapRestore.Services.Abstraction;

public interface IOverlayService
{
    /// <returns>True when the source was processed without overlay issues; false when the source was copied after an overlay failure.</returns>
    Task<bool> ApplyOverlayIfPresentAsync(
        string sourceFile,
        string destinationFile,
        string reportFile,
        CancellationToken cancellationToken = default);
}
