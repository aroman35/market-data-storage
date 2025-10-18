using System.Collections.Concurrent;
using System.Data;
using Banana.TardisDataLoader.Options;
using Banana.TardisDataLoader.Persistence.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Banana.TardisDataLoader.Persistence;

/// <summary>
/// Lightweight connection pool wrapper for Npgsql with hard cap via SemaphoreSlim.
/// Reuses opened connections, validates them on checkout, and returns them back on dispose.
/// </summary>
public sealed class PgConnectionPool : IPgConnectionPool
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentBag<NpgsqlConnection> _idle = new();

    private volatile bool _disposed;

    /// <summary>
    /// Create a new PgConnectionPool.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger">Serilog logger.</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public PgConnectionPool(IOptions<RepositoryOptions> options, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.ConnectionString, nameof(options.Value.ConnectionString));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Value.PoolSize, 0, nameof(options.Value.PoolSize));

        _connectionString = options.Value.ConnectionString;
        _logger = logger.ForContext<PgConnectionPool>() ?? throw new ArgumentNullException(nameof(logger));
        _semaphore = new SemaphoreSlim(options.Value.PoolSize, options.Value.PoolSize);

        _logger.Information("PgConnectionPool created. Size={PoolSize}", options.Value.PoolSize);
    }

    /// <summary>
    /// Lease an opened connection. Dispose the returned lease to return the connection to the pool.
    /// </summary>
    public async ValueTask<ConnectionLease> GetConnectionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        NpgsqlConnection? conn = null;
        try
        {
            if (_idle.TryTake(out conn))
            {
                // Ensure still valid/open.
                if (!IsOpen(conn))
                {
                    SafeDispose(conn);
                    conn = await CreateAndOpenAsync(ct).ConfigureAwait(false);
                }
            }
            else
            {
                conn = await CreateAndOpenAsync(ct).ConfigureAwait(false);
            }

            return new ConnectionLease(this, conn);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get connection from pool");
            // In case of failure make sure semaphore is released.
            _semaphore.Release();
            if (conn is null)
                SafeDispose(conn);
            throw;
        }
    }

    private async Task<NpgsqlConnection> CreateAndOpenAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        try
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);
            // Optional light ping (cheap no-op) to warm up / validate.
            // Using simple query is avoided; OpenAsync is sufficient.
            _logger.Debug("Opened new NpgsqlConnection. Idle={IdleCount}", _idle.Count);
            return conn;
        }
        catch
        {
            SafeDispose(conn);
            throw;
        }
    }

    private static bool IsOpen(NpgsqlConnection c)
        => c.State == ConnectionState.Open;

    private void Return(NpgsqlConnection conn)
    {
        if (_disposed)
        {
            SafeDispose(conn);
        }
        else if (IsOpen(conn))
        {
            _idle.Add(conn);
        }
        else
        {
            SafeDispose(conn);
        }

        _semaphore.Release();
    }

    /// <summary>
    /// Dispose the pool: prevents new leases and disposes all idle connections.
    /// Active leases will return and be disposed as they are released.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Drain idle and dispose
        while (_idle.TryTake(out var conn))
            SafeDispose(conn);

        // Optionally wait a bounded time for in-flight leases to come back.
        // Not strictly required; left out to avoid blocking shutdown.
        await Task.CompletedTask;

        _semaphore.Dispose();
        _logger.Information("PgConnectionPool disposed");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PgConnectionPool));
    }

    private static void SafeDispose(NpgsqlConnection? c)
    {
        try
        {
            c?.Dispose();
        }
        catch
        {
            /* swallow */
        }
    }

    /// <summary>
    /// A connection lease which returns the connection to the pool on dispose.
    /// </summary>
    public sealed class ConnectionLease : IAsyncDisposable, IDisposable
    {
        private PgConnectionPool? _owner;
        public NpgsqlConnection Connection { get; }

        internal ConnectionLease(PgConnectionPool owner, NpgsqlConnection connection)
        {
            _owner = owner;
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Synchronous dispose: returns connection to pool.
        /// </summary>
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Return(Connection);
        }

        /// <summary>
        /// Async dispose: returns connection to pool.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
