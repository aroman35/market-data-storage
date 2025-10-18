using System.Runtime.CompilerServices;
using Banana.TardisDataLoader.Persistence.Abstractions;
using Dapper;

namespace Banana.TardisDataLoader.Persistence;

/// <inheritdoc cref="IInstrumentsRepository" />
public class InstrumentsRepository(
    IPgConnectionPool connectionPool,
    ILogger logger) : RepositoryBase(connectionPool), IInstrumentsRepository
{
    private readonly ILogger _logger = logger.ForContext<InstrumentsRepository>();

    /// <inheritdoc />
    public async Task UpsertInstrumentsForExchange(
        Exchange exchange,
        IEnumerable<Instrument> instruments,
        CancellationToken cancellationToken)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        var rows = instruments
            .Select(MapUpsertParams)
            .ToArray();

        if (rows.Length == 0)
        {
            _logger.Information("No instruments to upsert for exchange {Exchange}", exchange);
            return;
        }

        await using var tx = await Connection!.BeginTransactionAsync(cancellationToken);
        try
        {
            // Dapper выполнит скрипт из файла 1 раз на каждый элемент rows
            var affected = await Connection.ExecuteAsync(
                SqlCommands.Instruments.UpsertOne,
                rows,
                tx);

            await tx.CommitAsync(cancellationToken);
            _logger.Information(
                "Upserted {Count} instruments for {Exchange}. Rows affected: {Affected}",
                rows.Length,
                exchange,
                affected);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.Error(ex, "UpsertInstrumentsForExchange failed for {Exchange}", exchange);
            throw;
        }

        // локальные мапперы
        static object MapUpsertParams(Instrument x)
        {
            var dbType = InstrumentTypeMapper.ToDbValue(x.Type);

            static DateTimeOffset? ToUtc(DateTime? dt)
            {
                if (dt is null)
                    return null;
                var v = dt.Value;
                if (v.Kind != DateTimeKind.Utc)
                    v = DateTime.SpecifyKind(v, DateTimeKind.Utc);
                return new DateTimeOffset(v);
            }

            return new
            {
                exchange_name = x.Exchange.ToString(),
                @base = x.BaseCurrency,
                @quote = x.QuoteCurrency,
                instrument_type = dbType,
                dataset = x.DatasetId,
                active = x.Active,
                available_since = ToUtc(x.AvailableSince),
                price_increment = x.PriceIncrement,
                amount_increment = x.AmountIncrement,
                min_trade_amount = x.MinTradeAmount,
                maker_fee = x.MakerFee,
                taker_fee = x.TakerFee,
                margin = x.Margin,
                inverse = x.Inverse,
                contract_multiplier = x.ContractMultiplier,
                listing = ToUtc(x.Listing),
                expiration = ToUtc(x.Expiry)
            };
        }
    }

    /// <inheritdoc />
    public async Task<KeyValuePair<Guid, Instrument>?> Get(
        string baseAsset,
        string quoteAsset,
        Exchange exchange,
        InstrumentType instrumentType,
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        DateTimeOffset? at = null;

        if (date is not null)
        {
            var d = date.Value;
            if (d.Kind != DateTimeKind.Utc)
                d = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            at = new DateTimeOffset(d);
        }

        var row = await Connection!.QuerySingleOrDefaultAsync<InstrumentDbRow>(
            SqlCommands.Instruments.GetCurrentInstrumentBySymbol,
            new
            {
                BaseAsset = baseAsset,
                QuoteAsset = quoteAsset,
                Exchange = exchange.ToString(),
                InstrumentType = instrumentType.ToString().ToLowerInvariant(),
                At = at
            });

        if (row is null)
            return null;
        return new KeyValuePair<Guid, Instrument>(row.InstrumentId, row.ToDomain());
    }

    /// <inheritdoc />
    public async Task<Instrument?> Get(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        var row = await Connection!.QuerySingleOrDefaultAsync<InstrumentDbRow>(
            SqlCommands.Instruments.GetCurrentInstrumentById,
            new { InstrumentId = instrumentId });

        return row?.ToDomain();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<KeyValuePair<Guid, Instrument>> ListInstrumentForExchange(
        Exchange exchange,
        InstrumentType instrumentType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureConnectionLease(cancellationToken);
        ArgumentNullException.ThrowIfNull(Connection);

        var rows = await Connection!.QueryAsync<InstrumentDbRow>(
            SqlCommands.Instruments.ListInstrumentsByType,
            new { Exchange = exchange.ToString(), InstrumentType = instrumentType.ToString().ToLowerInvariant() });

        foreach (var r in rows)
        {
            yield return new KeyValuePair<Guid, Instrument>(r.InstrumentId, r.ToDomain());
        }
    }

    private sealed record InstrumentDbRow
    {
        public Guid InstrumentId { get; init; }
        public Guid SymbolId { get; init; }

        public string BaseAsset { get; init; } = string.Empty;
        public string QuoteAsset { get; init; } = string.Empty;
        public string Exchange { get; init; } = string.Empty;
        public string InstrumentType { get; init; } = string.Empty;

        public string Dataset { get; init; } = string.Empty;
        public bool Active { get; init; }
        public DateTimeOffset? AvailableSince { get; init; }

        public decimal PriceIncrement { get; init; }
        public decimal AmountIncrement { get; init; }
        public decimal MinTradeAmount { get; init; }
        public decimal MakerFee { get; init; }
        public decimal TakerFee { get; init; }

        public bool? Margin { get; init; }
        public bool? Inverse { get; init; }
        public decimal? ContractMultiplier { get; init; }

        public DateTimeOffset? Listing { get; init; }
        public DateTimeOffset? Expiration { get; init; }

        public Instrument ToDomain()
        {
            return new Instrument
            {
                DatasetId = Dataset,
                Exchange = Enum.TryParse<Exchange>(Exchange, out var exVal) ? exVal : default,
                BaseCurrency = BaseAsset,
                QuoteCurrency = QuoteAsset,
                Type = InstrumentType,
                Active = Active,
                AvailableSince = AvailableSince?.UtcDateTime,
                PriceIncrement = PriceIncrement,
                AmountIncrement = AmountIncrement,
                MinTradeAmount = MinTradeAmount,
                MakerFee = MakerFee,
                TakerFee = TakerFee,
                Margin = Margin,
                Inverse = Inverse,
                ContractMultiplier = ContractMultiplier,
                Listing = Listing?.UtcDateTime,
                Expiry = Expiration?.UtcDateTime
            };
        }
    }

    private static class InstrumentTypeMapper
    {
        public static string ToDbValue(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Instrument type is null or empty", nameof(s));

            switch (s.Trim().ToLowerInvariant())
            {
                case "spot": return "spot";
                case "perpetual": return "perpetual";
                case "future": return "future";
                default: throw new ArgumentException($"Unknown instrument type: '{s}'", nameof(s));
            }
        }
    }
}
