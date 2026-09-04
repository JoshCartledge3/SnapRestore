using System;

namespace SnapRestore.Models;

public sealed class SnapchatMemoryHistoryItem
{
    public required DateTime DateUtc { get; init; }
    public required string MediaType { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public bool HasValidLocation =>
        Latitude is >= -90 and <= 90 &&
        Longitude is >= -180 and <= 180;
}
