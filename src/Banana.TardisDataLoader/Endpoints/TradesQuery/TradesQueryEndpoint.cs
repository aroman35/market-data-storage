using Banana.TardisDataLoader.Clients;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.TradesQuery;

public class TradesQueryEndpoint(TardisClient tardisClient) : Endpoint<TradesQueryRequest, ICollection<Trade>>
{
    public override void Configure()
    {
        Get("/market-data/trades");
        AllowAnonymous();
    }

    public override async Task<ICollection<Trade>> ExecuteAsync(TradesQueryRequest request, CancellationToken cancellationToken)
    {
        var trades = await tardisClient
            .DownloadTrades(request.Exchange, request.DatasetId, request.Date, cancellationToken)
            .Skip(request.Skip)
            .Take(request.Count)
            .ToArrayAsync(cancellationToken);

        return trades;
    }
}
