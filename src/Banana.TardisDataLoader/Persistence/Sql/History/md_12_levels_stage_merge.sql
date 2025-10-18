-- 1) TEMP dedup-таблица c PK по ключу дня
CREATE
TEMP TABLE IF NOT EXISTS level_updates_stage_dedup (
  ts            timestamptz    NOT NULL,
  instrument_id uuid           NOT NULL,
  side          smallint       NOT NULL,
  price         numeric(38,18) NOT NULL,
  quantity      numeric(38,18) NOT NULL,
  is_snapshot   boolean        NOT NULL,
  update_seq    integer        NOT NULL,
  trading_day   date           NOT NULL,
  PRIMARY KEY (instrument_id, trading_day, update_seq)
) ON COMMIT DROP;

-- 2) Наполняем dedup В ПРАВИЛЬНОМ ПОРЯДКЕ: ранние ts идут первыми → именно они останутся
INSERT INTO level_updates_stage_dedup (ts, instrument_id, side, price, quantity, is_snapshot, update_seq, trading_day)
SELECT ts,
       instrument_id,
       side,
       price,
       quantity,
       is_snapshot,
       update_seq,
       trading_day
FROM level_updates_stage
ORDER BY instrument_id, trading_day, update_seq, ts ON CONFLICT DO NOTHING;

-- 3) Пишем ключи в guard-таблицу (дедуп против уже записанных)
WITH keys AS (
INSERT
INTO md.level_update_keys (instrument_id, trading_day, update_seq)
SELECT instrument_id, trading_day, update_seq
FROM level_updates_stage_dedup ON CONFLICT DO NOTHING
  RETURNING instrument_id, trading_day, update_seq
)
-- 4) В боевую летят только НОВЫЕ ключи
INSERT
INTO md.level_updates (ts, instrument_id, side, price, quantity, is_snapshot, update_seq)
SELECT d.ts, d.instrument_id, d.side, d.price, d.quantity, d.is_snapshot, d.update_seq
FROM level_updates_stage_dedup d
         JOIN keys k USING (instrument_id, trading_day, update_seq);
