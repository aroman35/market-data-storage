using MediatR;

namespace Banana.TardisDataLoader.Features.LoadLevelUpdates;

public record LoadLevelUpdatesBySymbolRequest(
    string BaseAsset,
    string QuoteAsset,
    Exchange Exchange,
    InstrumentType InstrumentType,
    DateOnly Date) : IRequest;
