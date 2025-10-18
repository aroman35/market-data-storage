SELECT
    i.id                  AS instrument_id,
    i.symbol_id           AS symbol_id,
    a_base.name           AS base_asset,
    a_quote.name          AS quote_asset,
    ex.name               AS exchange,
    i.instrument_type     AS instrument_type,
    i.dataset             AS dataset,
    i.active              AS active,
    i.available_since     AS available_since,
    i.price_increment     AS price_increment,
    i.amount_increment    AS amount_increment,
    i.min_trade_amount    AS min_trade_amount,
    i.maker_fee           AS maker_fee,
    i.taker_fee           AS taker_fee,
    i.margin              AS margin,
    i.inverse             AS inverse,
    i.contract_multiplier AS contract_multiplier,
    i.listing             AS listing,
    i.expiration          AS expiration
FROM mkt.instruments i
         JOIN mkt.symbols  s     ON s.id = i.symbol_id
         JOIN mkt.exchanges ex   ON ex.id = s.exchange_id
         JOIN mkt.assets a_base  ON a_base.id = s.base_asset_id
         JOIN mkt.assets a_quote ON a_quote.id = s.quote_asset_id
WHERE ex.name = @Exchange
  AND i.instrument_type = @InstrumentType::mkt.instrument_type
  AND i.active = TRUE
  AND (i.expiration IS NULL OR now() < i.expiration)
ORDER BY a_base.name, a_quote.name;
