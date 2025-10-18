using Banana.TardisDataLoader.Persistence.Abstractions;
using FastEndpoints;

namespace Banana.TardisDataLoader.Endpoints.InstrumentQuery;

public class InstrumentQueryEndpoint(IInstrumentsRepository repository) : Endpoint<InstrumentQueryRequest, Instrument?>
{
    public override void Configure()
    {
        Get("/instruments");
        AllowAnonymous();
    }

    public override async Task<Instrument?> ExecuteAsync(InstrumentQueryRequest request, CancellationToken cancellationToken)
    {
        var instrument = await repository.Get(request.BaseAsset, request.QuoteAsset, request.Exchange, request.InstrumentType, request.Date, cancellationToken);
        return instrument?.Value;
    }
}
