namespace Banana.TardisDataLoader.Endpoints.LevelUpdatesQuery;

public record LevelUpdatesQueryRequest(Exchange Exchange, string DatasetId, DateOnly Date, int Count, int Skip = 0);
