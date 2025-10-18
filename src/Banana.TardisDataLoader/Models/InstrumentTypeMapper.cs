namespace Banana.TardisDataLoader.Models;

public static class InstrumentTypeMapper
{
    // строка из твоей доменной модели (Instrument.Type) -> enum (если сможем)
    public static bool TryParse(string? s, out InstrumentType t)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            t = default;
            return false;
        }

        switch (s.Trim().ToLowerInvariant())
        {
            case "spot":
                t = InstrumentType.Spot;
                return true;
            case "perpetual":
                t = InstrumentType.Perpetual;
                return true;
            case "future":
                t = InstrumentType.Future;
                return true;
            default:
                t = default;
                return false;
        }
    }

    // в строку для БД enum mkt.instrument_type
    public static string ToDbValue(InstrumentType t) =>
        t switch
        {
            InstrumentType.Spot => "spot",
            InstrumentType.Perpetual => "perpetual",
            InstrumentType.Future => "future",
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
        };
}
