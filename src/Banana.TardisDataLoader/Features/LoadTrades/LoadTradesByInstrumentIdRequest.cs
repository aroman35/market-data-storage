using MediatR;

namespace Banana.TardisDataLoader.Features.LoadTrades;

public record LoadTradesByInstrumentIdRequest(Guid InstrumentId, DateOnly Date) : IRequest;
