using System;

namespace SnapRestore.Models;

public sealed class MediaMetadata
{
    public DateTime? CreateDateUtc { get; init; }
    public DateTime? FileModifyDateUtc { get; init; }
}
