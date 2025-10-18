namespace Banana.TardisDataLoader.Models.Postgres;

public record TradeDto(DateTimeOffset Ts, Guid InstrumentId, short Side, decimal Price, decimal Quantity, long TradeId)
{
    public static TradeDto FromDomain(Trade trade, Guid instrumentId)
    {
        return new TradeDto(
            trade.Timestamp,
            instrumentId,
            (short)trade.Side,
            trade.Price,
            trade.Quantity,
            trade.Id);
    }
}
