CREATE TEMP TABLE IF NOT EXISTS level_updates_stage
(
    ts            timestamptz     NOT NULL,
    instrument_id uuid            NOT NULL,
    side          smallint        NOT NULL,
    price         numeric(38, 18) NOT NULL,
    quantity      numeric(38, 18) NOT NULL,
    is_snapshot   boolean         NOT NULL,
    update_seq    integer         NOT NULL
) ON COMMIT DROP;

-- Не обязателен, но помогает если много дублей:
-- CREATE INDEX IF NOT EXISTS ix_level_updates_stage_key
--   ON level_updates_stage (instrument_id, ts, update_seq);
