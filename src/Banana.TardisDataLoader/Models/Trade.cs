using Banana.TardisDataLoader.Extensions;

namespace Banana.TardisDataLoader.Models;

public record Trade(long Id, DateTimeOffset Timestamp, Side Side, decimal Price, decimal Quantity)
{
    public DateOnly TradeDate => Timestamp.ToUtcDateOnly();
    public TimeOnly Time => Timestamp.ToUtcTimeOnly();
}
