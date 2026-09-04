using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;

namespace SnapRestore.Services.Abstraction;

public interface IExternalProcessRunner
{
    Task<ExternalProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
