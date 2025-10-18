namespace Banana.TardisDataLoader.Models.Postgres;

public record LevelUpdateDto(
    DateTimeOffset Ts,
    Guid InstrumentId,
    short Side,
    decimal Price,
    decimal Quantity,
    bool IsSnapshot,
    int UpdateSeq)
{
    public static LevelUpdateDto FromDomain(LevelUpdate domain, Guid instrumentId, int sequence)
    {
        return new LevelUpdateDto(
            domain.Timestamp,
            instrumentId,
            (short)domain.Side,
            domain.Price,
            domain.Quantity,
            domain.IsSnapshot,
            sequence
            );
    }
}
