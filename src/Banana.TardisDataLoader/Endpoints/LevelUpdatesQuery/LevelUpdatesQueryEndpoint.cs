using Banana.TardisDataLoader.Clients;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.LevelUpdatesQuery;

public class LevelUpdatesQueryEndpoint(TardisClient tardisClient) : Endpoint<LevelUpdatesQueryRequest, ICollection<LevelUpdate>>
{
    public override void Configure()
    {
        Get("/market-data/level-updates");
        AllowAnonymous();
    }

    public override async Task<ICollection<LevelUpdate>> ExecuteAsync(LevelUpdatesQueryRequest request, CancellationToken cancellationToken)
    {
        var levelUpdates = await tardisClient
            .DownloadLevelUpdates(request.Exchange, request.DatasetId, request.Date, cancellationToken)
            .Skip(request.Skip)
            .Take(request.Count)
            .ToArrayAsync(cancellationToken);

        return levelUpdates;
    }
}
