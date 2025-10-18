using Banana.TardisDataLoader.Clients;
using Banana.TardisDataLoader.Persistence.Abstractions;
using MediatR;

namespace Banana.TardisDataLoader.Features.RefreshInstruments;

public class RefreshInstrumentsHandler(TardisClient tardisClient, IInstrumentsRepository instrumentsRepository) : IRequestHandler<RefreshInstrumentsCommand>
{
    public async Task Handle(RefreshInstrumentsCommand request, CancellationToken cancellationToken)
    {
        var instruments = await tardisClient
            .GetExchangeInstrumentsAsync(request.Exchange, cancellationToken)
            .ToArrayAsync(cancellationToken);
        await instrumentsRepository.UpsertInstrumentsForExchange(request.Exchange,  instruments, cancellationToken);
    }
}
