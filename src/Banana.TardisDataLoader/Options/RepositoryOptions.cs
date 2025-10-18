using Banana.TardisDataLoader.Persistence;
using Banana.TardisDataLoader.Persistence.Abstractions;

namespace Banana.TardisDataLoader.Options;

public class RepositoryOptions
{
    public int BatchSize { get; set; }
    public bool UseBinaryConnection { get; set; }
    public int PoolSize { get; set; }
    public string? ConnectionString { get; set; }
    public TimeSpan CommandTimeout { get; set; }
    public int WorkingMemoryMb { get; set; }

    public void ConfigureRepository(IServiceCollection services)
    {
        var descriptor = ServiceDescriptor.Scoped(
            typeof(IMarketDataRepository),
            UseBinaryConnection ? typeof(MarketDataBinaryRepository) : typeof(MarketDataBatchRepository));
        services.Add(descriptor);
        services.AddScoped<IInstrumentsRepository, InstrumentsRepository>();
    }
}
