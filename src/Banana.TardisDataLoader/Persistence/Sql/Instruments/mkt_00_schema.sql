CREATE SCHEMA IF NOT EXISTS mkt;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1
                       FROM pg_type t
                                JOIN pg_namespace n ON n.oid = t.typnamespace
                       WHERE t.typname = 'instrument_type'
                         AND n.nspname = 'mkt') THEN
            CREATE TYPE mkt.instrument_type AS ENUM ('perpetual','spot','future');
        END IF;
    END
$$;

CREATE TABLE IF NOT EXISTS mkt.exchanges
(
    id   bigserial PRIMARY KEY,
    name text NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS mkt.assets
(
    id   bigserial PRIMARY KEY,
    name text NOT NULL UNIQUE
);

-- символ БЕЗ типа
CREATE TABLE IF NOT EXISTS mkt.symbols
(
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    base_asset_id  bigint NOT NULL REFERENCES mkt.assets (id) ON DELETE CASCADE,
    quote_asset_id bigint NOT NULL REFERENCES mkt.assets (id) ON DELETE CASCADE,
    exchange_id    bigint NOT NULL REFERENCES mkt.exchanges (id) ON DELETE CASCADE,
    CONSTRAINT symbols_uq UNIQUE (base_asset_id, quote_asset_id, exchange_id)
);

-- одна строка на (symbol_id, instrument_type)
CREATE TABLE IF NOT EXISTS mkt.instruments
(
    id                  uuid PRIMARY KEY             DEFAULT gen_random_uuid(),
    symbol_id           uuid                NOT NULL REFERENCES mkt.symbols (id) ON DELETE CASCADE,
    instrument_type     mkt.instrument_type NOT NULL,

    dataset             text                NOT NULL,
    active              boolean             NOT NULL DEFAULT true,
    available_since     timestamptz         NULL,

    price_increment     numeric(38, 18)     NOT NULL CHECK (price_increment > 0),
    amount_increment    numeric(38, 18)     NOT NULL CHECK (amount_increment > 0),
    min_trade_amount    numeric(38, 18)     NOT NULL CHECK (min_trade_amount > 0),

    maker_fee           numeric(12, 8)      NOT NULL CHECK (maker_fee >= 0),
    taker_fee           numeric(12, 8)      NOT NULL CHECK (taker_fee >= 0),

    margin              boolean             NULL,
    inverse             boolean             NULL,
    contract_multiplier numeric(38, 18)     NULL CHECK (contract_multiplier IS NULL OR contract_multiplier > 0),

    listing             timestamptz         NULL,
    expiration          timestamptz         NULL,

    CONSTRAINT instruments_uq UNIQUE (symbol_id, instrument_type)
);

CREATE INDEX IF NOT EXISTS ix_symbols_exchange_bq
    ON mkt.symbols (exchange_id, base_asset_id, quote_asset_id);

CREATE INDEX IF NOT EXISTS ix_symbols_exchange
    ON mkt.symbols (exchange_id);

CREATE INDEX IF NOT EXISTS ix_instruments_symbol_type
    ON mkt.instruments (symbol_id, instrument_type);
