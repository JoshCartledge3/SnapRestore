using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public sealed class ExternalToolResolver : IExternalToolResolver
{
    public string GetExifToolPath()
    {
        return FindTool(IsWindows ? "exiftool.exe" : "exiftool")
               ?? "exiftool";
    }

    public string? GetFfmpegDirectory()
    {
        var ffmpegPath = FindTool(IsWindows ? "ffmpeg.exe" : "ffmpeg");

        return ffmpegPath is null
            ? null
            : Path.GetDirectoryName(ffmpegPath);
    }

    private static string? FindTool(string executableName)
    {
        foreach (var directory in GetCandidateDirectories())
        {
            var path = Path.Combine(directory, executableName);

            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var toolsDirectory = Path.Combine(baseDirectory, "Tools");

        yield return Path.Combine(toolsDirectory, RuntimeInformation.RuntimeIdentifier);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return Path.Combine(toolsDirectory, RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Path.Combine(toolsDirectory, RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "win-arm64"
                : "win-x64");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            yield return Path.Combine(toolsDirectory, RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64");
        }

        yield return toolsDirectory;
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
