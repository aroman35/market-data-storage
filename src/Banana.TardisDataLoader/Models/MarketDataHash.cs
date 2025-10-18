namespace Banana.TardisDataLoader.Models;

/// <summary>
/// Identity for MD on date
/// </summary>
/// <param name="BaseAsset">
/// Base asset code (e.g., <c>BTC</c>). Case-sensitive match against <c>mkt.assets.name</c>.
/// </param>
/// <param name="QuoteAsset">
/// Quote asset code (e.g., <c>USDT</c>). Case-sensitive match against <c>mkt.assets.name</c>.
/// </param>
/// <param name="Exchange">
/// Exchange identifier as a domain enum. Mapped to its canonical string
/// (e.g., <c>BinanceFutures</c>) and matched against <c>mkt.exchanges.name</c>.
/// </param>
/// <param name="InstrumentType">
/// Instrument type to resolve the instrument row (e.g., <see cref="InstrumentType.Spot"/>,
/// <see cref="InstrumentType.Perpetual"/>, <see cref="InstrumentType.Future"/>).
/// </param>
/// <param name="Date">
/// UTC calendar day to inspect. The query scans the interval
/// <c>[day 00:00:00Z, (day + 1) 00:00:00Z)</c>.
/// </param>
/// <param name="Feed">
/// Feed to analyze (supported: <see cref="FeedType.Trades"/>, <see cref="FeedType.LevelUpdates"/>).
/// Unsupported feeds (e.g., <see cref="FeedType.OrdersLog"/>) yield <c>null</c>.
/// </param>
public record MarketDataHash(string BaseAsset,
    string QuoteAsset,
    Exchange Exchange,
    InstrumentType InstrumentType,
    DateOnly Date,
    FeedType Feed);
