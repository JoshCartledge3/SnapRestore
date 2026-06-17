using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SnapRestore.Models;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.Services;

public sealed class MemoriesHistoryService : IMemoriesHistoryService
{
    public async Task<IReadOnlyList<SnapchatMemoryHistoryItem>> ParseAsync(
        string memoriesHistoryJsonPath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(memoriesHistoryJsonPath);

        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var items = new List<SnapchatMemoryHistoryItem>();

        foreach (var element in EnumerateHistoryItems(document.RootElement))
        {
            if (!element.TryGetProperty("Date", out var dateProperty))
                continue;

            if (!element.TryGetProperty("Media Type", out var mediaTypeProperty))
                continue;

            var dateText = dateProperty.GetString();
            var mediaType = mediaTypeProperty.GetString();

            if (string.IsNullOrWhiteSpace(dateText) ||
                string.IsNullOrWhiteSpace(mediaType))
                continue;

            if (!DateTime.TryParseExact(
                    dateText,
                    "yyyy-MM-dd HH:mm:ss 'UTC'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dateUtc))
            {
                continue;
            }

            var (latitude, longitude) = ReadLocation(element);

            items.Add(new SnapchatMemoryHistoryItem
            {
                DateUtc = dateUtc,
                MediaType = mediaType,
                Latitude = latitude,
                Longitude = longitude
            });
        }

        return items;
    }

    private static IEnumerable<JsonElement> EnumerateHistoryItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var propertyName in new[] { "Saved Media", "Memories", "Memories History" })
        {
            if (root.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Array)
            {
                return property.EnumerateArray();
            }
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                return property.Value.EnumerateArray();
            }
        }

        return [];
    }

    private static (double? Latitude, double? Longitude) ReadLocation(JsonElement element)
    {
        if (!element.TryGetProperty("Location", out var locationProperty))
            return (null, null);

        var location = locationProperty.GetString();

        if (string.IsNullOrWhiteSpace(location))
            return (null, null);

        const string prefix = "Latitude, Longitude:";

        if (!location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return (null, null);

        var coordinates = location[prefix.Length..].Trim().Split(',');

        if (coordinates.Length != 2)
            return (null, null);

        var latitudeParsed = double.TryParse(
            coordinates[0].Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var latitude);

        var longitudeParsed = double.TryParse(
            coordinates[1].Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var longitude);

        return latitudeParsed && longitudeParsed
            ? (latitude, longitude)
            : (null, null);
    }
}
