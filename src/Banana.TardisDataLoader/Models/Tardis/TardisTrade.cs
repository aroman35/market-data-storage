using Banana.TardisDataLoader.Extensions;
using CsvHelper.Configuration.Attributes;

namespace Banana.TardisDataLoader.Models.Tardis;

[Feed(FeedType.Trades)]
public class TardisTrade : ITardisMarketData<Trade>
{
    //exchange,symbol,timestamp,local_timestamp,id,side,price,amount
    [Name("exchange")]
    public string? Exchange { get; set; }
    [Name("symbol")]
    public string? Symbol { get; set; }
    [Name("timestamp")]
    public long Timestamp { get; set; }
    [Name("local_timestamp")]
    public long LocalTimestamp { get; set; }
    [Name("id")]
    public long Id { get; set; }
    [Name("side")]
    public string? Side { get; set; }
    [Name("price")]
    public decimal Price { get; set; }
    [Name("amount")]
    public decimal Amount { get; set; }

    public Trade ToDomain()
    {
        var model = this;
        return new Trade(
            model.Id,
            model.Timestamp.AsUnixMicroseconds(),
            model.Side?.StringAsTradeSide() ?? Models.Side.Undefined,
            model.Price,
            model.Amount);
    }
}
