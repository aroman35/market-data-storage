using Banana.TardisDataLoader.Persistence.Abstractions;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.MissingDaysQuery;

public class MissingDaysQueryEndpoint(IMarketDataRepository marketDataRepository) : Endpoint<MarketDataHash, IReadOnlyList<DateOnly>>
{
    public override void Configure()
    {
        Get("/market-data/missing-days");
        AllowAnonymous();
    }

    public override async Task<IReadOnlyList<DateOnly>> ExecuteAsync(MarketDataHash request, CancellationToken cancellationToken)
    {
        var dates = await marketDataRepository.ListMissingFeedDays(
            request,
            cancellationToken);
        return dates;
    }
}
