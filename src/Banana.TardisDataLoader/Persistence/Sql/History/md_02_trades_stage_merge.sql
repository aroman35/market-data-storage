-- Перелив из TEMP (pg_temp.trades_stage) в боевые таблицы с дедупом по торговому дню
WITH rows AS (SELECT ts,
                     instrument_id,
                     side,
                     price,
                     quantity,
                     trade_id,
                     (ts AT TIME ZONE 'UTC') ::date AS trading_day
              FROM trades_stage),
     chosen AS (SELECT DISTINCT
ON (instrument_id, trading_day, trade_id)
    ts, instrument_id, side, price, quantity, trade_id, trading_day
FROM rows
ORDER BY instrument_id, trading_day, trade_id, ts ASC
    ),
    keys AS (
INSERT
INTO md.trade_keys (instrument_id, trading_day, trade_id)
SELECT instrument_id, trading_day, trade_id
FROM chosen
ON CONFLICT DO NOTHING
    RETURNING instrument_id, trading_day, trade_id
    )
INSERT
INTO md.trades (ts, instrument_id, side, price, quantity, trade_id)
SELECT c.ts, c.instrument_id, c.side, c.price, c.quantity, c.trade_id
FROM chosen c
         JOIN keys k USING (instrument_id, trading_day, trade_id);
