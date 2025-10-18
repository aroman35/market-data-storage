using Banana.TardisDataLoader.Features.LoadTrades;
using FastEndpoints;
using MediatR;

namespace Banana.TardisDataLoader.Endpoints.LoadTradesCommand;

public class LoadTradesCommandEndpoint(IMediator mediator) : Endpoint<LoadTradesBySymbolCommandRequest>
{
    public override void Configure()
    {
        Post("/market-data/trades/launch");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoadTradesBySymbolCommandRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new LoadTradesBySymbolRequest(request.BaseAsset, request.QuoteAsset, request.Exchange, request.InstrumentType, request.Date), cancellationToken);
    }
}
