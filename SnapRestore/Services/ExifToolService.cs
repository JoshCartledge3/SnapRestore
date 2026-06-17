using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public sealed class ExifToolService(IExternalToolResolver externalToolResolver) : IExifToolService
{
    public async Task<MediaMetadata> ReadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = externalToolResolver.GetExifToolPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-json");
        startInfo.ArgumentList.Add("-CreateDate");
        startInfo.ArgumentList.Add("-FileModifyDate");
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add("%Y-%m-%d %H:%M:%S%z");
        startInfo.ArgumentList.Add(filePath);

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Failed to start exiftool.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"exiftool failed for '{filePath}'."
                    : error.Trim());
        }

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return new MediaMetadata();

        var metadata = root[0];

        return new MediaMetadata
        {
            CreateDateUtc = ReadExifDateUtc(metadata, "CreateDate"),
            FileModifyDateUtc = ReadExifDateUtc(metadata, "FileModifyDate")
        };
    }

    private static DateTime? ReadExifDateUtc(JsonElement metadata, string propertyName)
    {
        if (!metadata.TryGetProperty(propertyName, out var property))
            return null;

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:sszzz",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var dateWithOffset))
        {
            return dateWithOffset.UtcDateTime;
        }

        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dateWithoutOffset)
            ? DateTime.SpecifyKind(dateWithoutOffset, DateTimeKind.Utc)
            : null;
    }
    
    public async Task WriteGpsAsync(
        string filePath,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = externalToolResolver.GetExifToolPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-overwrite_original");

        if (IsVideoFile(filePath))
        {
            startInfo.ArgumentList.Add($"-Keys:GPSCoordinates={latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            var latitudeRef = latitude < 0 ? "S" : "N";
            var longitudeRef = longitude < 0 ? "W" : "E";
            var absoluteLatitude = Math.Abs(latitude).ToString(CultureInfo.InvariantCulture);
            var absoluteLongitude = Math.Abs(longitude).ToString(CultureInfo.InvariantCulture);

            startInfo.ArgumentList.Add($"-GPSLatitude={absoluteLatitude}");
            startInfo.ArgumentList.Add($"-GPSLatitudeRef={latitudeRef}");
            startInfo.ArgumentList.Add($"-GPSLongitude={absoluteLongitude}");
            startInfo.ArgumentList.Add($"-GPSLongitudeRef={longitudeRef}");
            startInfo.ArgumentList.Add($"-XMP:GPSLatitude={latitude.ToString(CultureInfo.InvariantCulture)}");
            startInfo.ArgumentList.Add($"-XMP:GPSLongitude={longitude.ToString(CultureInfo.InvariantCulture)}");
        }

        startInfo.ArgumentList.Add(filePath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start exiftool.");

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error.Trim());
        }
    }
    
    private static bool IsVideoFile(string path)
    {
        return Path.GetExtension(path)
            .Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }
}
