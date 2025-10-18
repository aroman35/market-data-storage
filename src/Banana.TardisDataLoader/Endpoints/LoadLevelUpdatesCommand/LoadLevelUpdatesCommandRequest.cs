namespace Banana.TardisDataLoader.Endpoints.LoadLevelUpdatesCommand;

public record LoadLevelUpdatesCommandRequest(
    string BaseAsset,
    string QuoteAsset,
    Exchange Exchange,
    InstrumentType InstrumentType,
    DateOnly Date);
