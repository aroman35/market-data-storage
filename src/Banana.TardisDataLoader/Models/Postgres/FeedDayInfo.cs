namespace Banana.TardisDataLoader.Models.Postgres;

public record FeedDayInfo(
    long ItemsCount,
    DateTimeOffset FirstTs,
    DateTimeOffset LastTs);
