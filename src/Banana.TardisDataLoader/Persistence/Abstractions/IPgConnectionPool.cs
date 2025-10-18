namespace Banana.TardisDataLoader.Persistence.Abstractions;

public interface IPgConnectionPool : IAsyncDisposable
{
    ValueTask<PgConnectionPool.ConnectionLease> GetConnectionAsync(CancellationToken ct = default);
}
