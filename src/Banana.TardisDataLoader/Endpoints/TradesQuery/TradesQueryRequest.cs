namespace Banana.TardisDataLoader.Endpoints.TradesQuery;

public record TradesQueryRequest(Exchange Exchange, string DatasetId, DateOnly Date, int Count, int Skip = 0);
