CREATE TEMP TABLE IF NOT EXISTS level_updates_raw
(
    ts            timestamptz      NOT NULL,
    instrument_id uuid             NOT NULL,
    side          md.side_smallint NOT NULL,
    price         numeric(38,18)   NOT NULL,
    quantity      numeric(38,18)   NOT NULL,
    is_snapshot   boolean          NOT NULL,
    update_seq    integer          NOT NULL
)
