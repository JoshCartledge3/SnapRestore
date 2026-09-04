using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public sealed class ExifToolService(
    IExternalToolResolver externalToolResolver,
    IExternalProcessRunner processRunner) : IExifToolService
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(30);

    public async Task<MediaMetadata> ReadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            externalToolResolver.GetExifToolPath(),
            [
                "-json",
                "-CreateDate",
                "-FileModifyDate",
                "-d",
                "%Y-%m-%d %H:%M:%S%z",
                filePath
            ],
            ReadTimeout,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"exiftool failed for '{filePath}'."
                    : result.StandardError.Trim());
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
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
    
    public async Task WriteMetadataAsync(
        string filePath,
        DateTime captureDateUtc,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default)
    {
        var arguments = new System.Collections.Generic.List<string> { "-overwrite_original" };
        var timestamp = captureDateUtc.ToUniversalTime().ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (IsVideoFile(filePath))
        {
            arguments.Add("-api");
            arguments.Add("QuickTimeUTC=1");
            arguments.Add($"-QuickTime:CreateDate={timestamp}");
            arguments.Add($"-QuickTime:ModifyDate={timestamp}");
            arguments.Add($"-TrackCreateDate={timestamp}");
            arguments.Add($"-TrackModifyDate={timestamp}");
            arguments.Add($"-MediaCreateDate={timestamp}");
            arguments.Add($"-MediaModifyDate={timestamp}");

            if (latitude is not null && longitude is not null)
            {
                arguments.Add(
                    $"-Keys:GPSCoordinates={latitude.Value.ToString(CultureInfo.InvariantCulture)},{longitude.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        else
        {
            arguments.Add($"-DateTimeOriginal={timestamp}");
            arguments.Add($"-CreateDate={timestamp}");
            arguments.Add($"-ModifyDate={timestamp}");

            if (latitude is not null && longitude is not null)
            {
                var latitudeRef = latitude < 0 ? "S" : "N";
                var longitudeRef = longitude < 0 ? "W" : "E";
                var absoluteLatitude = Math.Abs(latitude.Value).ToString(CultureInfo.InvariantCulture);
                var absoluteLongitude = Math.Abs(longitude.Value).ToString(CultureInfo.InvariantCulture);

                arguments.Add($"-GPSLatitude={absoluteLatitude}");
                arguments.Add($"-GPSLatitudeRef={latitudeRef}");
                arguments.Add($"-GPSLongitude={absoluteLongitude}");
                arguments.Add($"-GPSLongitudeRef={longitudeRef}");
                arguments.Add($"-XMP:GPSLatitude={latitude.Value.ToString(CultureInfo.InvariantCulture)}");
                arguments.Add($"-XMP:GPSLongitude={longitude.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        arguments.Add(filePath);

        var result = await processRunner.RunAsync(
            externalToolResolver.GetExifToolPath(),
            arguments,
            WriteTimeout,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"exiftool failed for '{filePath}'."
                    : result.StandardError.Trim());
        }

        File.SetLastWriteTimeUtc(filePath, captureDateUtc);
    }
    
    private static bool IsVideoFile(string path)
    {
        return Path.GetExtension(path)
            .Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }
}
