CREATE TEMP TABLE IF NOT EXISTS trades_stage
(
    ts            timestamptz     NOT NULL,
    instrument_id uuid            NOT NULL,
    side          smallint        NOT NULL,
    price         numeric(38, 18) NOT NULL,
    quantity      numeric(38, 18) NOT NULL,
    trade_id      bigint          NOT NULL
) ON COMMIT DROP;
