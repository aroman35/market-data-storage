using Banana.TardisDataLoader.Extensions;

namespace Banana.TardisDataLoader.Models;

public record LevelUpdate(DateTimeOffset Timestamp, Side Side, decimal Price, decimal Quantity, bool IsSnapshot)
{
    public DateOnly TradeDate => Timestamp.ToUtcDateOnly();
    public TimeOnly Time => Timestamp.ToUtcTimeOnly();
}
