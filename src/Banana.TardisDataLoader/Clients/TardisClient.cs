using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Banana.TardisDataLoader.Extensions;
using Banana.TardisDataLoader.Models.Tardis;
using Banana.TardisDataLoader.Options;
using CsvHelper;
using Flurl;
using Microsoft.Extensions.Options;

namespace Banana.TardisDataLoader.Clients;

public class TardisClient(
    HttpClient httpClient,
    IOptionsSnapshot<TardisHttpClientOptions> tardisOptions,
    ILogger logger)
{
    private static readonly Dictionary<FeedType, string> TardisFeedNames = new()
    {
        [FeedType.Trades] = "trades",
        [FeedType.LevelUpdates] = "incremental_book_L2",
    };

    private readonly ILogger _logger = logger.ForContext<TardisClient>();

    public async IAsyncEnumerable<Instrument> GetExchangeInstrumentsAsync(Exchange exchange, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tardisOptions.Value.ApiUrl);
        var url = tardisOptions.Value.ApiUrl
            .AppendPathSegment("v1/instruments")
            .AppendPathSegment(exchange.GetDescription());
        var response = await httpClient.GetAsync(url, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseStream = response.Content.ReadFromJsonAsAsyncEnumerable<TardisInstrument>(cancellationToken);
        await foreach (var instrumentRaw in responseStream)
        {
            if (instrumentRaw is not null)
                yield return instrumentRaw.ToDomain(exchange);
        }
    }

    public IAsyncEnumerable<Trade> DownloadTrades(Exchange exchange, string datasetId, DateOnly date, CancellationToken cancellation = default) =>
        DownloadDataset<Trade, TardisTrade>(exchange, datasetId, date, cancellation);

    public IAsyncEnumerable<LevelUpdate> DownloadLevelUpdates(Exchange exchange, string datasetId, DateOnly date, CancellationToken cancellation = default) =>
        DownloadDataset<LevelUpdate, TardisLevelUpdate>(exchange, datasetId, date, cancellation);

    private async IAsyncEnumerable<TMarketDataType> DownloadDataset<TMarketDataType, TTardisMarketDataType>(
        Exchange exchange,
        string datasetId,
        DateOnly date,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    where TTardisMarketDataType : class, ITardisMarketData<TMarketDataType>, new()
    {
        var feed = typeof(TTardisMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed ??
            throw new InvalidOperationException("Feed type attribute is not configured for the given data type");

        await using var tardisStream = await DownloadDatasetFileAsync(exchange, datasetId, date, feed, cancellationToken);
        await using (var decompressionStream = new GZipStream(tardisStream, CompressionMode.Decompress))
        {
            using (var reader = new StreamReader(decompressionStream, Encoding.UTF8))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var recordDefault = new TTardisMarketDataType();
                    await foreach (var record in csv.EnumerateRecordsAsync(recordDefault, cancellationToken))
                    {
                        yield return record.ToDomain();
                    }
                }
            }
        }
    }

    private async Task<Stream> DownloadDatasetFileAsync(
        Exchange exchange,
        string datasetId,
        DateOnly date,
        FeedType feed,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tardisOptions.Value.DatasetsUrl);
        if (!TardisFeedNames.TryGetValue(feed, out var feedName))
            throw new InvalidOperationException($"Tardis feed name is not configured for {feed}");

        var url = tardisOptions.Value.DatasetsUrl
            .AppendPathSegment("v1")
            .AppendPathSegment(exchange.GetDescription())
            .AppendPathSegment(feedName)
            .AppendPathSegment(date.Year.ToString("0000"))
            .AppendPathSegment(date.Month.ToString("00"))
            .AppendPathSegment(date.Day.ToString("00"))
            .AppendPathSegment(datasetId + ".csv.gz");

        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<TardisErrorResponse>(cancellationToken);
                if (!errorResponse?.IsTradingDate(date) ?? false)
                {
                    _logger.Error(
                        "Error downloading tardis data for {Symbol}: no data on date {Date}",
                        datasetId,
                        date);
                    return Stream.Null;
                }
            }
            throw new HttpRequestException($"Error downloading tardis data: {response.StatusCode}");
        }
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}
