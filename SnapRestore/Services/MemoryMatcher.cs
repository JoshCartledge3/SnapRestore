using System;
using System.Collections.Generic;
using System.Linq;
using SnapRestore.Models;

namespace SnapRestore.Services;

public sealed class MemoryMatcher
{
    private static readonly TimeSpan MatchTolerance = TimeSpan.FromMinutes(1);
    private readonly Dictionary<string, List<SnapchatMemoryHistoryItem>> _itemsByMediaType;
    private readonly HashSet<SnapchatMemoryHistoryItem> _usedItems = [];

    public MemoryMatcher(IEnumerable<SnapchatMemoryHistoryItem> items)
    {
        _itemsByMediaType = items
            .GroupBy(item => item.MediaType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.DateUtc).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public SnapchatMemoryHistoryItem? Match(string mediaType, params DateTime?[] candidateDatesUtc)
    {
        if (!_itemsByMediaType.TryGetValue(mediaType, out var items))
            return null;

        foreach (var candidateDate in candidateDatesUtc)
        {
            if (candidateDate is null)
                continue;

            var match = FindClosestUnused(items, candidateDate.Value);

            if (match is null)
                continue;

            _usedItems.Add(match);
            return match;
        }

        return null;
    }

    private SnapchatMemoryHistoryItem? FindClosestUnused(
        IReadOnlyList<SnapchatMemoryHistoryItem> items,
        DateTime target)
    {
        var lower = 0;
        var upper = items.Count;

        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            if (items[middle].DateUtc < target)
                lower = middle + 1;
            else
                upper = middle;
        }

        SnapchatMemoryHistoryItem? best = null;
        var bestDifference = TimeSpan.MaxValue;

        for (var index = lower - 1; index >= 0; index--)
        {
            var difference = target - items[index].DateUtc;
            if (difference > MatchTolerance)
                break;

            Consider(items[index], difference.Duration(), ref best, ref bestDifference);
        }

        for (var index = lower; index < items.Count; index++)
        {
            var difference = items[index].DateUtc - target;
            if (difference > MatchTolerance)
                break;

            Consider(items[index], difference.Duration(), ref best, ref bestDifference);
        }

        return best;
    }

    private void Consider(
        SnapchatMemoryHistoryItem item,
        TimeSpan difference,
        ref SnapchatMemoryHistoryItem? best,
        ref TimeSpan bestDifference)
    {
        if (_usedItems.Contains(item) || difference >= bestDifference)
            return;

        best = item;
        bestDifference = difference;
    }
}
