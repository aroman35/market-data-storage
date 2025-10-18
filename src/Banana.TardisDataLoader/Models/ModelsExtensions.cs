namespace Banana.TardisDataLoader.Models;

public static class ModelsExtensions
{
    public static Side StringAsTradeSide(this string value)
    {
        return value switch
        {
            "sell" => Side.Short,
            "buy" => Side.Long,
            _ => Side.Undefined
        };
    }

    public static Side StringAsOrderSide(this string value)
    {
        return value switch
        {
            "ask" => Side.Short,
            "bid" => Side.Long,
            _ => Side.Undefined
        };
    }
}
