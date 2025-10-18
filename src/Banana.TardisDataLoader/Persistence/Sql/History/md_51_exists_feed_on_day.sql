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
SELECT CASE
           WHEN @FeedType = 'LevelUpdates' THEN EXISTS (
               SELECT 1
               FROM md.level_updates lu
                        JOIN i ON i.id = lu.instrument_id
               WHERE lu.ts >= @Day::timestamptz
                   AND lu.ts <  (@Day::timestamptz + INTERVAL '1 day')
               LIMIT 1
           )
           WHEN @FeedType = 'Trades' THEN EXISTS (
               SELECT 1
               FROM md.trades t
                        JOIN i ON i.id = t.instrument_id
               WHERE t.ts >= @Day::timestamptz
                   AND t.ts <  (@Day::timestamptz + INTERVAL '1 day')
               LIMIT 1
           )
           ELSE FALSE
           END AS has_data;
