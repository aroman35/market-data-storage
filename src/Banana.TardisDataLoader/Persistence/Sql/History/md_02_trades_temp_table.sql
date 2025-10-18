CREATE TEMP TABLE IF NOT EXISTS trades_raw
(
    ts            timestamptz      NOT NULL,
    instrument_id uuid             NOT NULL,
    side          md.side_smallint NOT NULL,
    price         numeric(38,18)   NOT NULL,
    quantity      numeric(38,18)   NOT NULL,
    trade_id      bigint           NOT NULL
)
