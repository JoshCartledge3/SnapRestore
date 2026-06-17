namespace SnapRestore.Services.Abstraction;

public interface IExternalToolResolver
{
    string GetExifToolPath();

    string? GetFfmpegDirectory();
}
