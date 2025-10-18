namespace Banana.TardisDataLoader.Persistence.Abstractions;

/// <summary>
/// Repository abstraction for reference data (exchanges, assets, symbols) and instruments.
/// Designed for simple, idempotent upserts and straightforward lookups.
///
/// Model rules:
/// - <c>symbols</c> do NOT include instrument type.
/// - <c>instruments</c> link to a symbol and carry <c>instrument_type</c>.
/// - One row per (symbol, instrument_type). No versioning of instrument parameters.
/// </summary>
public interface IInstrumentsRepository
{
    /// <summary>
    /// Upserts a batch of instruments for the given exchange.
    /// For each input instrument:
    /// <list type="number">
    ///   <item><description>Ensures <c>exchange</c> exists (by name).</description></item>
    ///   <item><description>Ensures <c>base</c> and <c>quote</c> assets exist (by name).</description></item>
    ///   <item><description>Ensures <c>symbol</c> exists (base, quote, exchange).</description></item>
    ///   <item><description><c>INSERT ... ON CONFLICT (symbol_id, instrument_type) DO UPDATE</c> into <c>instruments</c>.</description></item>
    /// </list>
    /// Operation is idempotent and updates business fields on conflict.
    /// </summary>
    /// <param name="exchange">
    /// Target exchange (domain enum). Mapped to canonical string (e.g., <c>BinanceFutures</c>)
    /// and written/read as <c>mkt.exchanges.name</c>.
    /// </param>
    /// <param name="instruments">
    /// Sequence of domain instruments to upsert. Each instrument must provide:
    /// <c>DatasetId</c>, <c>BaseCurrency</c>, <c>QuoteCurrency</c>, <c>Type</c> (spot/perpetual/future),
    /// and optional business fields (fees, increments, listing/expiry, etc.).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the DB operation.</param>
    /// <returns>A task that completes when the upsert finishes (no count is returned).</returns>
    Task UpsertInstrumentsForExchange(
        Exchange exchange,
        IEnumerable<Instrument> instruments,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves an instrument by (base, quote, exchange, instrument type) and returns its identifier
    /// together with the mapped domain object.
    /// </summary>
    /// <param name="baseAsset">Base asset code (e.g., <c>BTC</c>). Case-sensitive match against <c>mkt.assets.name</c>.</param>
    /// <param name="quoteAsset">Quote asset code (e.g., <c>USDT</c>). Case-sensitive match against <c>mkt.assets.name</c>.</param>
    /// <param name="exchange">Exchange (domain enum). Mapped to <c>mkt.exchanges.name</c>.</param>
    /// <param name="instrumentType">
    /// Instrument type (e.g., <see cref="InstrumentType.Spot"/>, <see cref="InstrumentType.Perpetual"/>,
    /// <see cref="InstrumentType.Future"/>). Mapped to the DB enum <c>mkt.instrument_type</c>.
    /// </param>
    /// <param name="date">
    /// Optional date parameter. Currently ignored because instrument parameters are not versioned.
    /// Reserved for future compatibility.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the DB operation.</param>
    /// <returns>
    /// A key-value pair of <c>instrument_id</c> and domain <see cref="Instrument"/> if found; otherwise <c>null</c>.
    /// </returns>
    Task<KeyValuePair<Guid, Instrument>?> Get(
        string baseAsset,
        string quoteAsset,
        Exchange exchange,
        InstrumentType instrumentType,
        DateTime? date = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an instrument by its identifier.
    /// </summary>
    /// <param name="instrumentId">Instrument identifier (UUID) from <c>mkt.instruments.id</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the DB operation.</param>
    /// <returns>The domain <see cref="Instrument"/> if found; otherwise <c>null</c>.</returns>
    Task<Instrument?> Get(Guid instrumentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all instruments for a given exchange and instrument type.
    /// </summary>
    /// <param name="exchange">Exchange (domain enum). Mapped to <c>mkt.exchanges.name</c>.</param>
    /// <param name="instrumentType">
    /// Instrument type to filter by (e.g., <see cref="InstrumentType.Spot"/>,
    /// <see cref="InstrumentType.Perpetual"/>, <see cref="InstrumentType.Future"/>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the DB operation.</param>
    /// <returns>
    /// An asynchronous stream of key-value pairs: <c>instrument_id</c> and domain <see cref="Instrument"/>.
    /// </returns>
    IAsyncEnumerable<KeyValuePair<Guid, Instrument>> ListInstrumentForExchange(
        Exchange exchange,
        InstrumentType instrumentType,
        CancellationToken cancellationToken = default);
}
