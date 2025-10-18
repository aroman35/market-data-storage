using Banana.TardisDataLoader.Persistence.Abstractions;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.ListInstrumentsQuery;

public class ListInstrumentsQueryEndpoint(IInstrumentsRepository repository) : Endpoint<ListInstrumentsQueryRequest, ICollection<Instrument>>
{
    public override void Configure()
    {
        Get("/instruments/list");
        AllowAnonymous();
    }

    public override async Task<ICollection<Instrument>> ExecuteAsync(ListInstrumentsQueryRequest request, CancellationToken cancellationToken)
    {
        var instruments = await repository
            .ListInstrumentForExchange(request.Exchange, request.InstrumentType, cancellationToken)
            .Select(x => x.Value)
            .ToArrayAsync(cancellationToken);
        return instruments;
    }
}
