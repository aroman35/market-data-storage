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
    ),
    cal AS (
SELECT gs::date AS d
FROM generate_series(
    @StartDate::date,
    (now() AT TIME ZONE 'UTC')::date,
    interval '1 day'
    ) AS gs
    ),
    present AS (
/* Levels */
SELECT DISTINCT (lu.ts AT TIME ZONE 'UTC')::date AS d
FROM md.level_updates lu
    JOIN i ON i.id = lu.instrument_id
WHERE @FeedType = 'LevelUpdates'
UNION ALL
/* Trades */
SELECT DISTINCT (t.ts AT TIME ZONE 'UTC')::date AS d
FROM md.trades t
    JOIN i ON i.id = t.instrument_id
WHERE @FeedType = 'Trades'
    ),
    missing AS (
SELECT c.d
FROM cal c
    LEFT JOIN present p ON p.d = c.d
WHERE p.d IS NULL
    )
SELECT d
FROM missing
ORDER BY d ASC;
