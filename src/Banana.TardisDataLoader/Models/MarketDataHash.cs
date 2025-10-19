namespace Banana.TardisDataLoader.Models;

/// <summary>
/// Identity for MD on date.
/// </summary>
public sealed class MarketDataHash
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataHash"/> class.
    /// Parameterless constructor is useful for serializers.
    /// </summary>
    public MarketDataHash()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataHash"/> class.
    /// </summary>
    /// <param name="baseAsset">
    /// Base asset code (e.g., <c>BTC</c>). Case-sensitive match against <c>mkt.assets.name</c>.
    /// </param>
    /// <param name="quoteAsset">
    /// Quote asset code (e.g., <c>USDT</c>). Case-sensitive match against <c>mkt.assets.name</c>.
    /// </param>
    /// <param name="exchange">
    /// Exchange identifier as a domain enum. Mapped to its canonical string
    /// (e.g., <c>BinanceFutures</c>) and matched against <c>mkt.exchanges.name</c>.
    /// </param>
    /// <param name="instrumentType">
    /// Instrument type to resolve the instrument row (e.g., <see cref="InstrumentType.Spot"/>,
    /// <see cref="InstrumentType.Perpetual"/>, <see cref="InstrumentType.Future"/>).
    /// </param>
    /// <param name="date">
    /// UTC calendar day to inspect. The query scans the interval
    /// <c>[day 00:00:00Z, (day + 1) 00:00:00Z)</c>.
    /// </param>
    /// <param name="feed">
    /// Feed to analyze (supported: <see cref="FeedType.Trades"/>, <see cref="FeedType.LevelUpdates"/>).
    /// Unsupported feeds (e.g., <see cref="FeedType.OrdersLog"/>) yield <c>null</c>.
    /// </param>
    public MarketDataHash(
        string baseAsset,
        string quoteAsset,
        Exchange exchange,
        InstrumentType instrumentType,
        DateOnly date,
        FeedType feed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteAsset);

        BaseAsset = baseAsset;
        QuoteAsset = quoteAsset;
        Exchange = exchange;
        InstrumentType = instrumentType;
        Date = date;
        Feed = feed;
    }

    /// <summary>
    /// Base asset code (e.g., <c>BTC</c>). Case-sensitive match against <c>mkt.assets.name</c>.
    /// </summary>
    public string BaseAsset { get; set; } = string.Empty;

    /// <summary>
    /// Quote asset code (e.g., <c>USDT</c>). Case-sensitive match against <c>mkt.assets.name</c>.
    /// </summary>
    public string QuoteAsset { get; set; } = string.Empty;

    /// <summary>
    /// Exchange identifier as a domain enum. Mapped to its canonical string
    /// (e.g., <c>BinanceFutures</c>) and matched against <c>mkt.exchanges.name</c>.
    /// </summary>
    public Exchange Exchange { get; set; }

    /// <summary>
    /// Instrument type to resolve the instrument row (e.g., <see cref="InstrumentType.Spot"/>,
    /// <see cref="InstrumentType.Perpetual"/>, <see cref="InstrumentType.Future"/>).
    /// </summary>
    public InstrumentType InstrumentType { get; set; }

    /// <summary>
    /// UTC calendar day to inspect. The query scans the interval
    /// <c>[day 00:00:00Z, (day + 1) 00:00:00Z)</c>.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Feed to analyze (supported: <see cref="FeedType.Trades"/>, <see cref="FeedType.LevelUpdates"/>).
    /// Unsupported feeds (e.g., <see cref="FeedType.OrdersLog"/>) yield <c>null</c>.
    /// </summary>
    public FeedType Feed { get; set; }
}
