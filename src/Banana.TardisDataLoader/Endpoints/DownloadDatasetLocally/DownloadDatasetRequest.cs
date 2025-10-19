namespace Banana.TardisDataLoader.Endpoints.DownloadDatasetLocally;

public class DownloadDatasetRequest
{
    public string BaseAsset { get; set; } = string.Empty;
    public string QuoteAsset { get; set; } = string.Empty;
    public Exchange Exchange { get; set; }
    public InstrumentType InstrumentType { get; set; }
    public DateOnly Date { get; set; }

    public FeedType Feed { get; set; }
}
