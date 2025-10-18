using Banana.TardisDataLoader.Features.LoadTrades;

namespace Banana.TardisDataLoader.Endpoints.LoadTradesCommand;

public record LoadTradesBySymbolCommandRequest(
    string BaseAsset,
    string QuoteAsset,
    Exchange Exchange,
    InstrumentType InstrumentType,
    DateOnly Date) : LoadTradesBySymbolRequest(BaseAsset, QuoteAsset, Exchange, InstrumentType, Date);
