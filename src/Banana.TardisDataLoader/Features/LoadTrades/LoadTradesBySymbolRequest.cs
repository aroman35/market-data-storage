using MediatR;

namespace Banana.TardisDataLoader.Features.LoadTrades;

public record LoadTradesBySymbolRequest(
    string BaseAsset,
    string QuoteAsset,
    Exchange Exchange,
    InstrumentType InstrumentType,
    DateOnly Date) : IRequest;
