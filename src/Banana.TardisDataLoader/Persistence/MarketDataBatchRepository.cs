using Banana.TardisDataLoader.Extensions;
using Banana.TardisDataLoader.Models.Postgres;
using Banana.TardisDataLoader.Options;
using Banana.TardisDataLoader.Persistence.Abstractions;
using Dapper;
using Microsoft.Extensions.Options;

namespace Banana.TardisDataLoader.Persistence;

/// <inheritdoc cref="IMarketDataRepository" />
public class MarketDataBatchRepository(
    IPgConnectionPool connectionPool,
    IOptionsSnapshot<RepositoryOptions> repositoryOptions,
    ILogger logger) : RepositoryBase(connectionPool), IMarketDataRepository
{
    private protected readonly int WorkingMemoryMb = repositoryOptions.Value.WorkingMemoryMb;
    private protected readonly int CommandTimeoutSeconds = (int)repositoryOptions.Value.CommandTimeout.TotalSeconds;
    private readonly int _batchSize = repositoryOptions.Value.BatchSize;
    private readonly ILogger _logger = logger.ForContext<MarketDataBatchRepository>();

    /// <inheritdoc />
    public virtual async Task<int> InsertTrades(
    Guid instrumentId,
    IAsyncEnumerable<Trade> trades,
    CancellationToken cancellationToken)
{
    await EnsureConnectionLease(cancellationToken);
    ArgumentNullException.ThrowIfNull(Connection);

    await using var transaction = await Connection.BeginTransactionAsync(cancellationToken);
    try
    {
        await Connection.ExecuteAsync(SqlCommands.SetLocalSynchronousCommitOff, transaction: transaction);
        await Connection.ExecuteAsync(SqlCommands.SetLocalWorkingMemory(WorkingMemoryMb), transaction: transaction);

        var stream = trades.Select(x => TradeDto.FromDomain(x, instrumentId)).ChunkAsync(_batchSize);
        var total = 0;

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            if (chunk.Length == 0)
                continue;

            var commandParameters = new DynamicParameters();
            commandParameters.Add("@Ts", chunk.Select(x => x.Ts).ToArray());
            commandParameters.Add("@InstrumentId", chunk.Select(x => x.InstrumentId).ToArray());
            commandParameters.Add("@Side", chunk.Select(x => x.Side).ToArray());
            commandParameters.Add("@Price", chunk.Select(x => x.Price).ToArray());
            commandParameters.Add("@Quantity", chunk.Select(x => x.Quantity).ToArray());
            commandParameters.Add("@TradeId", chunk.Select(x => x.TradeId).ToArray());

            var cmd = new CommandDefinition(
                SqlCommands.HistoricData.Trades.InsertBatch,
                commandParameters,
                transaction,
                commandTimeout: CommandTimeoutSeconds,
                cancellationToken: cancellationToken);

            total += await Connection.ExecuteAsync(cmd);
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.Information("{Count} trades inserted (UNNEST)", total);
        return total;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(cancellationToken);
        _logger.Error(ex, "InsertTrades (UNNEST) failed");
        throw;
    }
}


    /// <inheritdoc />
    public virtual async Task<int> InsertLevelUpdates(
    Guid instrumentId,
    IAsyncEnumerable<LevelUpdate> levelUpdates,
    CancellationToken cancellationToken)
{
    await EnsureConnectionLease(cancellationToken);
    ArgumentNullException.ThrowIfNull(Connection);

    await using var transaction = await Connection.BeginTransactionAsync(cancellationToken);
    try
    {
        await Connection.ExecuteAsync(SqlCommands.SetLocalSynchronousCommitOff, transaction: transaction);
        await Connection.ExecuteAsync(SqlCommands.SetLocalWorkingMemory(WorkingMemoryMb), transaction: transaction);

        var total = 0;
        var stream = levelUpdates
            .Select((model, seq) => LevelUpdateDto.FromDomain(model, instrumentId, seq))
            .ChunkAsync(_batchSize);

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            if (chunk.Length == 0)
                continue;

            var commandParameters = new DynamicParameters();
            commandParameters.Add("@Ts", chunk.Select(x => x.Ts).ToArray());
            commandParameters.Add("@InstrumentId", chunk.Select(x => x.InstrumentId).ToArray());
            commandParameters.Add("@Side", chunk.Select(x => x.Side).ToArray());
            commandParameters.Add("@Price", chunk.Select(x => x.Price).ToArray());
            commandParameters.Add("@Quantity", chunk.Select(x => x.Quantity).ToArray());
            commandParameters.Add("@IsSnapshot", chunk.Select(x => x.IsSnapshot).ToArray());
            commandParameters.Add("@UpdateSeq", chunk.Select(x => x.UpdateSeq).ToArray());

            var cmd = new CommandDefinition(
                SqlCommands.HistoricData.LevelUpdates.InsertLevelsBatch,
                commandParameters,
                transaction,
                commandTimeout: CommandTimeoutSeconds,
                cancellationToken: cancellationToken);

            total += await Connection.ExecuteAsync(cmd);
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.Information("{Count} level updates inserted (UNNEST)", total);
        return total;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(cancellationToken);
        _logger.Error(ex, "InsertLevelUpdates (UNNEST) failed");
        throw;
    }
}

    /// <inheritdoc />
    public async Task<IReadOnlyList<DateOnly>> ListMissingFeedDays(
        MarketDataHash hash,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        var args = new
        {
            BaseAsset = hash.BaseAsset,
            QuoteAsset = hash.QuoteAsset,
            Exchange = hash.Exchange.ToString(),
            InstrumentType = hash.InstrumentType.ToString().ToLowerInvariant(),
            Day = hash.Date.ToDateTime(TimeOnly.MinValue).Date,   // передаём как DATE
            FeedType = hash.Feed.ToString()
        };

        var dates = await Connection!.QueryAsync<DateTime>(
            SqlCommands.HistoricData.Utils.ListMissingDays,
            args);

        // вернуть DateOnly ASC
        return dates
            .Select(d => DateOnly.FromDateTime(DateTime.SpecifyKind(d, DateTimeKind.Utc)))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsFeedOnDay(
        MarketDataHash hash,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        // Unsupported feeds (например, OrdersLog) — сразу false
        if (hash.Feed is FeedType.Unknown or FeedType.OrdersLog)
            return false;

        var args = new
        {
            BaseAsset = hash.BaseAsset,
            QuoteAsset = hash.QuoteAsset,
            Exchange = hash.Exchange.ToString(),
            InstrumentType = hash.InstrumentType.ToString().ToLowerInvariant(),
            Day = hash.Date.ToDateTime(TimeOnly.MinValue).Date,   // передаём как DATE
            FeedType = hash.Feed.ToString()
        };

        var exists = await Connection!.ExecuteScalarAsync<bool>(
            SqlCommands.HistoricData.Utils.ExistsFeedOnDay,
            args);

        return exists;
    }

    /// <inheritdoc />
    public async Task<FeedDayInfo?> GetFeedDayInfo(
        MarketDataHash hash,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        if (hash.Feed is FeedType.Unknown or FeedType.OrdersLog)
            return null;

        // Границы UTC: [day, day+1)
        var fromUtc = hash.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        var args = new
        {
            BaseAsset = hash.BaseAsset,
            QuoteAsset = hash.QuoteAsset,
            Exchange = hash.Exchange.ToString(),
            InstrumentType = hash.InstrumentType.ToString().ToLowerInvariant(),
            FromUtc = fromUtc,
            ToUtc = toUtc
        };

        var sql = hash.Feed switch
        {
            FeedType.Trades => SqlCommands.HistoricData.Utils.GetTradesDayInfo,
            FeedType.LevelUpdates => SqlCommands.HistoricData.Utils.GetLevelsDayInfo,
            _ => null
        };

        if (sql is null)
            return null;

        var row = await Connection!.QuerySingleOrDefaultAsync<FeedDayInfoRow>(sql, args);

        return row is null
            ? null
            : new FeedDayInfo(
                ItemsCount: row.ItemsCount,
                FirstTs: DateTime.SpecifyKind(row.FirstTs.UtcDateTime, DateTimeKind.Utc),
                LastTs: DateTime.SpecifyKind(row.LastTs.UtcDateTime, DateTimeKind.Utc));
    }

    private sealed class FeedDayInfoRow
    {
        public long ItemsCount { get; init; }
        public DateTimeOffset FirstTs { get; init; }
        public DateTimeOffset LastTs { get; init; }
    }
}
