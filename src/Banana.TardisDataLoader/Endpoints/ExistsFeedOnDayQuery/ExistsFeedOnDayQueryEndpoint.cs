using Banana.TardisDataLoader.Persistence.Abstractions;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.ExistsFeedOnDayQuery;

public class ExistsFeedOnDayQueryEndpoint(IMarketDataRepository marketDataRepository) : Endpoint<MarketDataHash, bool>
{
    public override void Configure()
    {
        Get("/market-data/exists");
        AllowAnonymous();
    }

    public override async Task<bool> ExecuteAsync(MarketDataHash request, CancellationToken cancellationToken)
    {
        var isExists = await marketDataRepository.ExistsFeedOnDay(
            request,
            cancellationToken);

        return isExists;
    }
}
