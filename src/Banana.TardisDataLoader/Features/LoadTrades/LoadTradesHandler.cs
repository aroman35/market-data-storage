using System.Diagnostics;
using Banana.TardisDataLoader.Clients;
using Banana.TardisDataLoader.Persistence.Abstractions;
using MediatR;

namespace Banana.TardisDataLoader.Features.LoadTrades;

public class LoadTradesHandler(
    TardisClient tardisClient,
    IMarketDataRepository marketDataBatchRepository,
    IInstrumentsRepository instrumentsRepository,
    ILogger logger) :
    IRequestHandler<LoadTradesByInstrumentIdRequest>,
    IRequestHandler<LoadTradesBySymbolRequest>
{
    private readonly ILogger _logger = logger.ForContext<LoadTradesHandler>();

    public async Task Handle(LoadTradesByInstrumentIdRequest request, CancellationToken cancellationToken)
    {
        var instrument = await instrumentsRepository.Get(request.InstrumentId, cancellationToken);
        if (instrument is null)
        {
            _logger.Warning("Instrument was not found for {Request}", request);
            return;
        }

        await DownloadAndBatchInsert(instrument, request.InstrumentId, request.Date, cancellationToken);
    }

    public async Task Handle(LoadTradesBySymbolRequest request, CancellationToken cancellationToken)
    {
        var instrument = await instrumentsRepository.Get(
            request.BaseAsset,
            request.QuoteAsset,
            request.Exchange,
            request.InstrumentType,
            null,
            cancellationToken);

        if (instrument is null)
        {
            _logger.Warning("Instrument was not found for {Request}", request);
            return;
        }

        await DownloadAndBatchInsert(instrument.Value.Value, instrument.Value.Key, request.Date, cancellationToken);
    }

    private async Task DownloadAndBatchInsert(
        Instrument? instrument,
        Guid instrumentId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(instrument?.DatasetId);
        var started = Stopwatch.GetTimestamp();
        var levelUpdates =
            tardisClient.DownloadTrades(instrument.Exchange, instrument.DatasetId, date, cancellationToken);
        var insertedCount = await marketDataBatchRepository.InsertTrades(instrumentId, levelUpdates, cancellationToken);
        _logger.Information(
            "[{Dataset}] ({Date}) {Count} trades downloaded and saved to DB in {Elapsed}",
            instrument.DatasetId,
            date,
            insertedCount,
            Stopwatch.GetElapsedTime(started));
    }
}
