-- Вставка батчом без COPY (через UNNEST массивов)
-- Конфликт гасим по уникалке (instrument_id, ts, update_seq)
INSERT INTO md.level_updates (ts, instrument_id, side, price, quantity, is_snapshot, update_seq)
SELECT ts, instrument_id, side, price, quantity, is_snapshot, update_seq
FROM (SELECT u.ts::timestamptz     AS ts,
             u.instrument_id::uuid AS instrument_id,
             u.side::smallint      AS side,
             u.price::numeric      AS price,
             u.quantity::numeric   AS quantity,
             u.is_snapshot::bool   AS is_snapshot,
             u.update_seq::int     AS update_seq
      FROM unnest(
                   @Ts::timestamptz[],
                   @InstrumentId::uuid[],
                   @Side::smallint[],
                   @Price::numeric[],
                   @Quantity::numeric[],
                   @IsSnapshot::bool[],
                   @UpdateSeq::int[]
           ) AS u(ts, instrument_id, side, price, quantity, is_snapshot, update_seq)
      ORDER BY instrument_id, ts) AS rows
ON CONFLICT (instrument_id, ts, update_seq) DO NOTHING;
