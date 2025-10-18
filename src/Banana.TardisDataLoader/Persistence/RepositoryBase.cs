using Banana.TardisDataLoader.Persistence.Abstractions;
using Npgsql;

namespace Banana.TardisDataLoader.Persistence;

/// <summary>
/// Base class for repository implementations that need access to PostgreSQL connections.
/// Provides a lazily-acquired connection lease from <see cref="IPgConnectionPool"/> and
/// configures Dapper to map snake_case column names to PascalCase properties.
/// </summary>
/// <remarks>
/// Usage:
/// <list type="number">
///   <item><description>Call <see cref="EnsureConnectionLease"/> at the start of each public method to obtain a pooled connection.</description></item>
///   <item><description>Use <c>Connection</c> for executing commands/queries (it is null until a lease is acquired).</description></item>
///   <item><description>Dispose the repository (or use <c>await using</c>) to release the lease back to the pool.</description></item>
/// </list>
/// Thread-safety: instances are not thread-safe; create one per scope/request.
/// </remarks>
public abstract class RepositoryBase(IPgConnectionPool connectionPool) : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Static initializer sets the global Dapper setting so that columns like
    /// <c>first_ts</c> map to properties like <c>FirstTs</c>.
    /// </summary>
    static RepositoryBase()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private PgConnectionPool.ConnectionLease? _connectionLease;

    /// <summary>
    /// The active pooled connection for this repository instance, or <c>null</c> if a lease
    /// has not yet been acquired. Call <see cref="EnsureConnectionLease"/> before use.
    /// </summary>
    protected NpgsqlConnection? Connection => _connectionLease?.Connection;

    /// <summary>
    /// Ensures a connection lease is acquired from the pool. If a lease already exists,
    /// this method is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel waiting for a pooled connection.</param>
    protected async Task EnsureConnectionLease(CancellationToken cancellationToken)
    {
        _connectionLease ??= await connectionPool.GetConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously disposes the repository and returns the connection lease to the pool.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Disposes the repository and returns the connection lease to the pool.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connectionLease?.Dispose();
    }
}
