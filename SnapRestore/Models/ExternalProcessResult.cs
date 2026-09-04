namespace SnapRestore.Models;

public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError);
