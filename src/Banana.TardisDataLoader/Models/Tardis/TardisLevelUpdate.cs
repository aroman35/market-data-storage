using Banana.TardisDataLoader.Extensions;
using CsvHelper.Configuration.Attributes;

namespace Banana.TardisDataLoader.Models.Tardis;

[Feed(FeedType.LevelUpdates)]
public class TardisLevelUpdate : ITardisMarketData<LevelUpdate>
{
    // exchange,symbol,timestamp,local_timestamp,is_snapshot,side,price,amount
    [Name("exchange")]
    public string? Exchange { get; set; }
    [Name("symbol")]
    public string? Symbol { get; set; }
    [Name("timestamp")]
    public long Timestamp { get; set; }
    [Name("local_timestamp")]
    public long LocalTimestamp { get; set; }
    [Name("is_snapshot")]
    public bool IsSnapshot { get; set; }
    [Name("side")]
    public string? Side { get; set; }
    [Name("price")]
    public decimal Price { get; set; }
    [Name("amount")]
    public decimal Amount { get; set; }

    public LevelUpdate ToDomain()
    {
        var model = this;
        return new LevelUpdate(
            model.Timestamp.AsUnixMicroseconds(),
            model.Side?.StringAsOrderSide() ?? Models.Side.Undefined,
            model.Price,
            model.Amount,
            model.IsSnapshot);
    }
}
