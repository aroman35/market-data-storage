-- ВАЖНО: без указания схемы (TEMP живёт в pg_temp)
CREATE
TEMP TABLE IF NOT EXISTS level_updates_stage (
  ts            timestamptz    NOT NULL,
  instrument_id uuid           NOT NULL,
  side          smallint       NOT NULL,
  price         numeric(38,18) NOT NULL,
  quantity      numeric(38,18) NOT NULL,
  is_snapshot   boolean        NOT NULL,
  update_seq    integer        NOT NULL,
  -- предвычисляем торговый день (UTC), чтобы не считать его много раз
  trading_day   date GENERATED ALWAYS AS ((ts AT TIME ZONE 'UTC')::date) STORED
) ON COMMIT DROP;

-- лёгкий многоколонный индекс под сортировку/джоины в merge
CREATE INDEX IF NOT EXISTS ix_level_updates_stage_key
    ON level_updates_stage (instrument_id, trading_day, update_seq, ts);
