DROP SCHEMA IF EXISTS md CASCADE;

CREATE SCHEMA md;
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- ===================== TRADES =====================
CREATE TABLE md.trades
(
    ts            timestamptz     NOT NULL,
    instrument_id uuid            NOT NULL REFERENCES mkt.instruments (id) ON DELETE RESTRICT,
    side          smallint        NOT NULL, -- -1/0/1
    price         numeric(38, 18) NOT NULL CHECK (price > 0),
    quantity      numeric(38, 18) NOT NULL CHECK (quantity >= 0),
    trade_id      bigint          NOT NULL  -- уникально по (instrument_id, ts, trade_id)
);

SELECT create_hypertable('md.trades', 'ts',
                         partitioning_column=>'instrument_id',
                         number_partitions=>64,
                         chunk_time_interval=>INTERVAL '1 day',
                         if_not_exists=> TRUE);

-- быстрые выборки по инструменту
CREATE INDEX IF NOT EXISTS ix_trades_instr_ts_desc ON md.trades (instrument_id, ts DESC);

-- ВАЖНО: UNIQUE содержит и instrument_id (space), и ts (time) → Timescale доволен
CREATE UNIQUE INDEX IF NOT EXISTS uq_trades_instr_ts_trade
    ON md.trades (instrument_id, ts, trade_id);

-- ===================== LEVEL UPDATES =====================
CREATE TABLE md.level_updates
(
    ts            timestamptz     NOT NULL,
    instrument_id uuid            NOT NULL REFERENCES mkt.instruments (id) ON DELETE RESTRICT,
    side          smallint        NOT NULL, -- -1 ask, 1 bid (0 если надо)
    price         numeric(38, 18) NOT NULL CHECK (price >= 0),
    quantity      numeric(38, 18) NOT NULL CHECK (quantity >= 0),
    is_snapshot   boolean         NOT NULL DEFAULT false,
    update_seq    integer         NOT NULL  -- твой инкремент (обычно «в пределах дня»), но UNIQUE будет по ts
);

SELECT create_hypertable('md.level_updates', 'ts',
                         partitioning_column=>'instrument_id',
                         number_partitions=>64,
                         chunk_time_interval=>INTERVAL '1 day',
                         if_not_exists=> TRUE);

CREATE INDEX IF NOT EXISTS ix_levels_instr_ts_desc ON md.level_updates (instrument_id, ts DESC);

-- Тут тоже: ts обязан быть в ключе. Берём (instrument_id, ts, update_seq)
CREATE UNIQUE INDEX IF NOT EXISTS uq_levels_instr_ts_seq
    ON md.level_updates (instrument_id, ts, update_seq);
