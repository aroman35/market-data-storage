using Banana.TardisDataLoader.Models.Postgres;
using Banana.TardisDataLoader.Persistence.Abstractions;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.MarketDataInfoQuery;

public class MarketDataQueryEndpoint(IMarketDataRepository marketDataRepository) : Endpoint<MarketDataHash, FeedDayInfo?>
{
    public override void Configure()
    {
        Get("/market-data/info");
        AllowAnonymous();
    }

    public override async Task<FeedDayInfo?> ExecuteAsync(MarketDataHash request, CancellationToken cancellationToken)
    {
        var info = await marketDataRepository.GetFeedDayInfo(request, cancellationToken);
        return info;
    }
}
