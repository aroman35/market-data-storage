using Banana.TardisDataLoader.Models.Postgres;

namespace Banana.TardisDataLoader.Persistence.Abstractions;

/// <summary>
/// Repository abstraction for reading and writing market data to PostgreSQL/TimescaleDB.
/// Optimized for high-throughput ingestion (100M+ level updates/day) and simple, predictable reads.
/// </summary>
public interface IMarketDataRepository
{
    /// <summary>
    /// Inserts a sequence of executed trades for a specific instrument.
    /// The implementation batches items and performs a set-based insert into a Timescale hypertable.
    /// </summary>
    /// <param name="instrumentId">
    /// Instrument identifier (UUID) in the <c>mkt.instruments</c> table. All incoming trades
    /// will be attached to this instrument. Must exist in the database.
    /// </param>
    /// <param name="trades">
    /// Asynchronous sequence of trades. Each trade should carry:
    /// <list type="bullet">
    ///   <item><description><c>Timestamp</c> (UTC recommended; will be treated as UTC in storage)</description></item>
    ///   <item><description><c>Side</c> (<see cref="Side.Long"/> or <see cref="Side.Short"/>)</description></item>
    ///   <item><description><c>Price</c> (decimal)</description></item>
    ///   <item><description><c>Quantity</c> (decimal)</description></item>
    ///   <item><description><c>Id</c> (provider trade id; unique per instrument)</description></item>
    /// </list>
    /// The repository deduplicates by <c>(instrument_id, trade_id)</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token controlling streaming, batching, and the underlying DB commands.
    /// </param>
    /// <returns>
    /// Total number of rows successfully inserted (deduplicated).
    /// </returns>
    /// <remarks>
    /// Performance tips:
    /// <list type="bullet">
    ///   <item><description>Send trades in increasing timestamp order to minimize page splits.</description></item>
    ///   <item><description>Use reasonable batch sizes (e.g., 10k) for best throughput.</description></item>
    /// </list>
    /// </remarks>
    Task<int> InsertTrades(
        Guid instrumentId,
        IAsyncEnumerable<Trade> trades,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a sequence of order book level updates (L2/L3 style) for a specific instrument.
    /// The implementation batches items and performs a set-based insert into a Timescale hypertable.
    /// </summary>
    /// <param name="instrumentId">
    /// Instrument identifier (UUID) in the <c>mkt.instruments</c> table. All incoming updates
    /// will be attached to this instrument. Must exist in the database.
    /// </param>
    /// <param name="levelUpdates">
    /// Asynchronous sequence of level updates. Each update should carry:
    /// <list type="bullet">
    ///   <item><description><c>Timestamp</c> (UTC recommended; will be treated as UTC in storage)</description></item>
    ///   <item><description><c>Side</c> (<see cref="Side.Long"/> for bids, <see cref="Side.Short"/> for asks)</description></item>
    ///   <item><description><c>Price</c> (decimal)</description></item>
    ///   <item><description><c>Quantity</c> (decimal)</description></item>
    ///   <item><description><c>IsSnapshot</c> (true for snapshot rows)</description></item>
    ///   <item><description><c>UpdateSeq</c> (int; caller-assigned monotonic sequence within the trading day)</description></item>
    /// </list>
    /// The repository deduplicates with a key of <c>(instrument_id, ts, update_seq)</c> or equivalent anti-join strategy.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token controlling streaming, batching, and the underlying DB commands.
    /// </param>
    /// <returns>
    /// Total number of rows successfully inserted (deduplicated).
    /// </returns>
    /// <remarks>
    /// Performance tips:
    /// <list type="bullet">
    ///   <item><description>Order updates by <c>(Timestamp, UpdateSeq)</c> to reduce index fragmentation.</description></item>
    ///   <item><description>Use 10k+ batch sizes; avoid tiny batches for high-volume symbols.</description></item>
    /// </list>
    /// </remarks>
    Task<int> InsertLevelUpdates(
        Guid instrumentId,
        IAsyncEnumerable<LevelUpdate> levelUpdates,
        CancellationToken cancellationToken);

    /// <summary>
    /// Produces an ascending list of UTC dates for which the specified feed has no data,
    /// starting from (and including) <paramref name="startDate"/> up to the current UTC day.
    /// </summary>
    /// <param name="hash">MD identity</param>
    /// <param name="cancellationToken">
    /// Cancellation token for the operation.
    /// </param>
    /// <returns>
    /// An ascending list of <see cref="DateOnly"/> representing UTC dates with no data for the specified feed.
    /// </returns>
    /// <remarks>
    /// If the instrument (symbol + type) cannot be resolved, the method returns all dates from
    /// <paramref name="hash.Date"/> through today (UTC) as missing.
    /// </remarks>
    Task<IReadOnlyList<DateOnly>> ListMissingFeedDays(
        MarketDataHash hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether there is any data for the specified feed on the exact UTC day
    /// (interval [day, day+1) in UTC) for the given instrument (resolved by base/quote/exchange/type).
    /// </summary>
    /// <param name="hash">MD identity</param>
    /// <param name="cancellationToken">
    /// Cancellation token for the operation.
    /// </param>
    /// <returns>
    /// <c>true</c> if at least one row exists for the given instrument and day; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Requires an index on <c>(instrument_id, ts)</c> for both trades and level updates tables to be efficient.
    /// </remarks>
    Task<bool> ExistsFeedOnDay(
        MarketDataHash hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns daily feed statistics for a specific instrument and UTC day:
    /// total item count, first timestamp, and last timestamp. If no rows exist
    /// for the specified day, the method returns <c>null</c>.
    /// </summary>
    /// <param name="hash">MD identity</param>
    /// <param name="cancellationToken">
    /// Cancellation token for the operation.
    /// </param>
    /// <returns>
    /// A <see cref="FeedDayInfo"/> with item count and first/last timestamps when data exists for the given day;
    /// otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Efficient with indexes on <c>(instrument_id, ts)</c>. Timestamps are stored/returned in UTC.
    /// </remarks>
    Task<FeedDayInfo?> GetFeedDayInfo(
        MarketDataHash hash,
        CancellationToken cancellationToken = default);
}
