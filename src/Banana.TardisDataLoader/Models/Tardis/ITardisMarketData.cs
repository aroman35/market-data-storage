namespace Banana.TardisDataLoader.Models.Tardis;

public interface ITardisMarketData<out TDomain>
{
    TDomain ToDomain();
}
