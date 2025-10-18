namespace Banana.TardisDataLoader.Endpoints.InstrumentQuery;

public record InstrumentQueryRequest(
    string BaseAsset,
    string QuoteAsset,
    Exchange Exchange,
    InstrumentType InstrumentType = InstrumentType.Perpetual,
    DateTime? Date = null);
