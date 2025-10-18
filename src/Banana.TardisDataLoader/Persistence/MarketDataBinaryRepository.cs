using Banana.TardisDataLoader.Models.Postgres;
using Banana.TardisDataLoader.Options;
using Banana.TardisDataLoader.Persistence.Abstractions;
using Dapper;
using Microsoft.Extensions.Options;
using NpgsqlTypes;

namespace Banana.TardisDataLoader.Persistence;

/// <inheritdoc />
public class MarketDataBinaryRepository(
    IPgConnectionPool connectionPool,
    IOptionsSnapshot<RepositoryOptions> repositoryOptions,
    ILogger logger) : MarketDataBatchRepository(connectionPool, repositoryOptions, logger)
{
    private readonly ILogger _logger = logger.ForContext<MarketDataBinaryRepository>();

    /// <inheritdoc />
    public override async Task<int> InsertTrades(
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
            await Connection.ExecuteAsync(SqlCommands.HistoricData.Trades.StageCreate, transaction: transaction);

            await using (var importer = await Connection.BeginBinaryImportAsync(
                             SqlCommands.HistoricData.Trades.BeginBinaryImport,
                             cancellationToken))
            {
                await foreach (var dto in trades
                                   .Select(x => TradeDto.FromDomain(x, instrumentId))
                                   .WithCancellation(cancellationToken))
                {
                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(dto.Ts.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
                    await importer.WriteAsync(instrumentId, NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(dto.Side, NpgsqlDbType.Smallint, cancellationToken);
                    await importer.WriteAsync(dto.Price, NpgsqlDbType.Numeric, cancellationToken);
                    await importer.WriteAsync(dto.Quantity, NpgsqlDbType.Numeric, cancellationToken);
                    await importer.WriteAsync(dto.TradeId, NpgsqlDbType.Bigint, cancellationToken);
                }

                await importer.CompleteAsync(cancellationToken);
            }

            var cmd = new CommandDefinition(
                SqlCommands.HistoricData.Trades.StageMerge,
                transaction: transaction,
                commandTimeout: CommandTimeoutSeconds
            );
            var inserted = await Connection.ExecuteAsync(cmd);

            await transaction.CommitAsync(cancellationToken);
            _logger.Information("{Count} trades inserted via COPY/merge", inserted);
            return inserted;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "InsertTradesFastAsync failed");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> InsertLevelUpdates(
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
            await Connection.ExecuteAsync(SqlCommands.HistoricData.LevelUpdates.StageCreate, transaction: transaction);

            await using (var importer = await Connection.BeginBinaryImportAsync(
                             SqlCommands.HistoricData.LevelUpdates.BeginBinaryImport,
                             cancellationToken))
            {
                await foreach (var dto in levelUpdates
                                   .Select((level, seq) => LevelUpdateDto.FromDomain(level, instrumentId, seq))
                                   .WithCancellation(cancellationToken))
                {
                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(dto.Ts.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
                    await importer.WriteAsync(instrumentId, NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(dto.Side, NpgsqlDbType.Smallint, cancellationToken);
                    await importer.WriteAsync(dto.Price, NpgsqlDbType.Numeric, cancellationToken);
                    await importer.WriteAsync(dto.Quantity, NpgsqlDbType.Numeric, cancellationToken);
                    await importer.WriteAsync(dto.IsSnapshot, NpgsqlDbType.Boolean, cancellationToken);
                    await importer.WriteAsync(dto.UpdateSeq, NpgsqlDbType.Integer, cancellationToken);
                }

                await importer.CompleteAsync(cancellationToken);
            }

            var cmd = new CommandDefinition(
                SqlCommands.HistoricData.LevelUpdates.StageMerge,
                transaction: transaction,
                commandTimeout: CommandTimeoutSeconds
            );
            var inserted = await Connection.ExecuteAsync(cmd);

            await transaction.CommitAsync(cancellationToken);
            _logger.Information("{Count} level updates inserted via COPY/merge", inserted);
            return inserted;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "InsertLevelUpdatesFastAsync failed");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
