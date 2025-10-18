WITH i AS (
    SELECT i.id
    FROM mkt.instruments i
             JOIN mkt.symbols  s     ON s.id = i.symbol_id
             JOIN mkt.exchanges ex   ON ex.id = s.exchange_id
             JOIN mkt.assets a_base  ON a_base.id = s.base_asset_id
             JOIN mkt.assets a_quote ON a_quote.id = s.quote_asset_id
    WHERE a_base.name = @BaseAsset
      AND a_quote.name = @QuoteAsset
      AND ex.name      = @Exchange
      AND i.instrument_type = @InstrumentType::mkt.instrument_type
    LIMIT 1
    )
SELECT *
FROM (
         SELECT
             COUNT(*)::bigint  AS items_count,
             MIN(t.ts)         AS first_ts,
             MAX(t.ts)         AS last_ts
         FROM md.trades t
                  JOIN i ON i.id = t.instrument_id
         WHERE t.ts >= @FromUtc
           AND t.ts <  @ToUtc
     ) s
WHERE s.items_count > 0;
