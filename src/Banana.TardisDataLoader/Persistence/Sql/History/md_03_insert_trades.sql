-- Вставка батчом без COPY (через UNNEST массивов)
-- Конфликт гасим по уникалке (instrument_id, ts, trade_id)
INSERT INTO md.trades (ts, instrument_id, side, price, quantity, trade_id)
SELECT ts, instrument_id, side, price, quantity, trade_id
FROM (SELECT u.ts::timestamptz     AS ts,
             u.instrument_id::uuid AS instrument_id,
             u.side::smallint      AS side,
             u.price::numeric      AS price,
             u.quantity::numeric   AS quantity,
             u.trade_id::bigint    AS trade_id
      FROM unnest(
                   @Ts::timestamptz[],
                   @InstrumentId::uuid[],
                   @Side::smallint[],
                   @Price::numeric[],
                   @Quantity::numeric[],
                   @TradeId::bigint[]
           ) AS u(ts, instrument_id, side, price, quantity, trade_id)
      ORDER BY instrument_id, ts) AS rows
ON CONFLICT (instrument_id, ts, trade_id) DO NOTHING;
