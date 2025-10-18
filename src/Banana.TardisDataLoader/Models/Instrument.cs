namespace Banana.TardisDataLoader.Models;

public class Instrument
{
    public string? DatasetId { get; set; }
    public Exchange Exchange { get; set; }
    public string? BaseCurrency { get; set; }
    public string? QuoteCurrency { get; set; }
    public string? Type { get; set; }
    public bool Active { get; set; }
    public DateTime? AvailableSince { get; set; }
    public decimal PriceIncrement { get; set; }
    public decimal AmountIncrement { get; set; }
    public decimal MinTradeAmount { get; set; }
    public decimal MakerFee { get; set; }
    public decimal TakerFee { get; set; }
    public bool? Margin { get; set; }
    public bool? Inverse { get; set; }
    public decimal? ContractMultiplier { get; set; }
    public DateTime? Listing { get; set; }
    public DateTime? Expiry { get; set; }
}
