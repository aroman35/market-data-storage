using System.Diagnostics;
using Banana.TardisDataLoader.Clients;
using Banana.TardisDataLoader.Persistence.Abstractions;
using MediatR;

namespace Banana.TardisDataLoader.Features.LoadLevelUpdates;

public class LoadLevelUpdatesHandler(
    TardisClient tardisClient,
    IMarketDataRepository marketDataBatchRepository,
    IInstrumentsRepository instrumentsRepository,
    ILogger logger) :
    IRequestHandler<LoadLevelUpdatesByInstrumentIdRequest>,
    IRequestHandler<LoadLevelUpdatesBySymbolRequest>
{
    private readonly ILogger _logger = logger.ForContext<LoadLevelUpdatesHandler>();

    public async Task Handle(LoadLevelUpdatesByInstrumentIdRequest request, CancellationToken cancellationToken)
    {
        var instrument = await instrumentsRepository.Get(request.InstrumentId, cancellationToken);
        if (instrument is null)
        {
            _logger.Warning("Instrument was not found for {Request}", request);
            return;
        }

        await DownloadAndBatchInsert(instrument, request.InstrumentId, request.Date, cancellationToken);
    }

    public async Task Handle(LoadLevelUpdatesBySymbolRequest request, CancellationToken cancellationToken)
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
            tardisClient.DownloadLevelUpdates(instrument.Exchange, instrument.DatasetId, date, cancellationToken);
        var insertedCount =
            await marketDataBatchRepository.InsertLevelUpdates(instrumentId, levelUpdates, cancellationToken);
        _logger.Information(
            "[{Dataset}] ({Date}) {Count} Level updates downloaded and saved to DB in {Elapsed}",
            instrument.DatasetId,
            date,
            insertedCount,
            Stopwatch.GetElapsedTime(started));
    }
}
