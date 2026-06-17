using System;

namespace SnapRestore.Models;

public sealed class SnapchatMemoryHistoryItem
{
    public required DateTime DateUtc { get; init; }
    public required string MediaType { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public bool HasValidLocation =>
        Latitude is not null &&
        Longitude is not null &&
        Latitude != 0 &&
        Longitude != 0;
}
