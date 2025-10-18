using Banana.TardisDataLoader.Features.LoadLevelUpdates;
using FastEndpoints;
using MediatR;

namespace Banana.TardisDataLoader.Endpoints.LoadLevelUpdatesCommand;

public class LoadLevelUpdatesCommandEndpoint(IMediator mediator) : Endpoint<LoadLevelUpdatesCommandRequest>
{
    public override void Configure()
    {
        Post("/market-data/level-updates/launch");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoadLevelUpdatesCommandRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new LoadLevelUpdatesBySymbolRequest(request.BaseAsset, request.QuoteAsset, request.Exchange, request.InstrumentType, request.Date), cancellationToken);
    }
}
