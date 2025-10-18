using Banana.TardisDataLoader.Features.RefreshInstruments;
using FastEndpoints;
using MediatR;

namespace Banana.TardisDataLoader.Endpoints.RefreshInstruments;

public class RefreshInstrumentsEndpoint(IMediator mediator) : Endpoint<RefreshInstrumentsRequest>
{
    public override void Configure()
    {
        Post("/instruments/refresh");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RefreshInstrumentsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RefreshInstrumentsCommand(request.Exchange),  cancellationToken);
    }
}
