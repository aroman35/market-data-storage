using System.Diagnostics;
using Banana.TardisDataLoader.Clients;
using Banana.TardisDataLoader.Options;
using Banana.TardisDataLoader.Persistence.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Banana.TardisDataLoader.Features.SaveDatasetFile;

public class SaveDatasetFileHandler(
    TardisClient tardisClient,
    IInstrumentsRepository instrumentsRepository,
    IOptionsSnapshot<LocalCacheOptions> options,
    ILogger logger) :
    IRequestHandler<SaveDatasetFileByInstrumentIdRequest>,
    IRequestHandler<SaveDatasetFileBySymbolRequest>
{
    private readonly ILogger _logger = logger.ForContext<SaveDatasetFileHandler>();

    public async Task Handle(SaveDatasetFileByInstrumentIdRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var started = Stopwatch.GetTimestamp();
            var instrument = await instrumentsRepository.Get(request.InstrumentId, cancellationToken);
            if (instrument is null)
            {
                _logger.Warning("Instrument was not found for {Request}", request);
                return;
            }
            await DownloadAndSaveDataset(instrument.Exchange, instrument.DatasetId, request.Date, request.Feed, cancellationToken);
            var duration = Stopwatch.GetElapsedTime(started);
            _logger.Information(
                "[{Dataset}] ({Date}-{Feed}) downloaded and saved locally in {Elapsed}",
                instrument.DatasetId,
                request.Date,
                request.Feed,
                duration);
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Something has gone wrong while downloading dataset for {Request}",
                request);
            throw;
        }
    }

    public async Task Handle(SaveDatasetFileBySymbolRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var started = Stopwatch.GetTimestamp();
            var instrumentResponse = await instrumentsRepository.Get(
                request.Hash.BaseAsset,
                request.Hash.QuoteAsset,
                request.Hash.Exchange,
                request.Hash.InstrumentType,
                null,
                cancellationToken);
            if (instrumentResponse is null)
            {
                _logger.Warning("Instrument was not found for {Request}", request);
                return;
            }

            var datasetId = instrumentResponse.Value.Value.DatasetId;
            await DownloadAndSaveDataset(request.Hash.Exchange, datasetId, request.Hash.Date, request.Hash.Feed, cancellationToken);

            var duration = Stopwatch.GetElapsedTime(started);
            _logger.Information(
                "[{Dataset}] ({Date}-{Feed}) downloaded and saved locally in {Elapsed}",
                datasetId,
                request.Hash.Date,
                request.Hash.Feed,
                duration);
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Something has gone wrong while downloading dataset for {Request}",
                request);
            throw;
        }
    }

    private async Task DownloadAndSaveDataset(
        Exchange exchange,
        string? datasetId,
        DateOnly date,
        FeedType feed,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.CacheDirectory);

        Directory.CreateDirectory(options.Value.CacheDirectory);
        var localDirectory = Directory.CreateDirectory(options.Value.CacheDirectory);

        var outputFileName = $"{exchange.ToString()}_{datasetId}_{date:O}_{feed.ToString()}.csv.gz";
        var outputFilePath = Path.Combine(localDirectory.FullName, outputFileName);
        if (File.Exists(outputFilePath))
            File.Delete(outputFilePath);

        var fileOptions = new FileStreamOptions
        {
            Access = FileAccess.Write,
            BufferSize = 1 << 20,
            Mode = FileMode.CreateNew,
            Options = FileOptions.Asynchronous,
            Share = FileShare.None
        };

        await using var outputFileStream = new FileStream(outputFilePath, fileOptions);
        await using var dataset = await tardisClient.DownloadDatasetFileAsync(exchange, datasetId, date, feed, cancellationToken);
        await dataset.CopyToAsync(outputFileStream, cancellationToken);
    }
}
