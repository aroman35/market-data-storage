using Banana.TardisDataLoader.Features.SaveDatasetFile;
using FastEndpoints;
using MediatR;

namespace Banana.TardisDataLoader.Endpoints.DownloadDatasetLocally;

public class DownloadDatasetLocallyEndpoint(IMediator mediator) : Endpoint<DownloadDatasetRequest>
{
    public override void Configure()
    {
        Post("/market-data/download");
        AllowAnonymous();
        Options(o =>
        {
            o.Accepts<DownloadDatasetRequest>("application/json");
            o.ProducesProblemFE();
        });
        Summary(s =>
        {
            s.Summary = "Downloads/locally caches market data archive by symbol+type+date.";
            s.Description = "Triggers dataset download for the given (base, quote, exchange, instrument type, date, feed).";
            s.ExampleRequest = new DownloadDatasetRequest
            {
                BaseAsset = "BTC",
                QuoteAsset = "USDT",
                Exchange = Exchange.BinanceFutures,
                InstrumentType = InstrumentType.Perpetual,
                Date = new DateOnly(2025, 9, 1),
                Feed = FeedType.LevelUpdates
            };
        });
    }

    public override async Task HandleAsync(DownloadDatasetRequest request,
        CancellationToken cancellationToken)
    {
        var hash = new MarketDataHash(
            baseAsset: request.BaseAsset,
            quoteAsset: request.QuoteAsset,
            exchange: request.Exchange,
            instrumentType: request.InstrumentType,
            date: request.Date,
            feed: request.Feed);
        await mediator.Send(new SaveDatasetFileBySymbolRequest(hash), cancellationToken);
    }
}
