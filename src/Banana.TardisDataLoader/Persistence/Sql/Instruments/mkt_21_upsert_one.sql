WITH ex_ins AS (
    INSERT INTO mkt.exchanges (name) VALUES (@exchange_name)
        ON CONFLICT (name) DO NOTHING
        RETURNING id),
     ex_id AS (SELECT COALESCE((SELECT id FROM ex_ins),
                               (SELECT id FROM mkt.exchanges WHERE name = @exchange_name)) AS id),
     a_base_ins AS (
         INSERT INTO mkt.assets (name) VALUES (@base)
             ON CONFLICT (name) DO NOTHING
             RETURNING id),
     a_quote_ins AS (
         INSERT INTO mkt.assets (name) VALUES (@quote)
             ON CONFLICT (name) DO NOTHING
             RETURNING id),
     ab AS (SELECT COALESCE((SELECT id FROM a_base_ins), (SELECT id FROM mkt.assets WHERE name = @base)) AS id),
     aq AS (SELECT COALESCE((SELECT id FROM a_quote_ins), (SELECT id FROM mkt.assets WHERE name = @quote)) AS id),
     sym_ins AS (
         INSERT INTO mkt.symbols (base_asset_id, quote_asset_id, exchange_id)
             SELECT ab.id, aq.id, ex_id.id
             FROM ab,
                  aq,
                  ex_id
             ON CONFLICT DO NOTHING
             RETURNING id),
     sym AS (SELECT COALESCE(
                            (SELECT id FROM sym_ins),
                            (SELECT s.id
                             FROM mkt.symbols s
                             WHERE s.base_asset_id = (SELECT id FROM ab)
                               AND s.quote_asset_id = (SELECT id FROM aq)
                               AND s.exchange_id = (SELECT id FROM ex_id))
                    ) AS id)
INSERT
INTO mkt.instruments (symbol_id,
                      instrument_type,
                      dataset,
                      active,
                      available_since,
                      price_increment,
                      amount_increment,
                      min_trade_amount,
                      maker_fee,
                      taker_fee,
                      margin,
                      inverse,
                      contract_multiplier,
                      listing,
                      expiration)
SELECT (SELECT id FROM sym),
       @instrument_type::mkt.instrument_type,
       @dataset,
       @active,
       @available_since,
       @price_increment,
       @amount_increment,
       @min_trade_amount,
       @maker_fee,
       @taker_fee,
       @margin,
       @inverse,
       @contract_multiplier,
       @listing,
       @expiration
ON CONFLICT (symbol_id, instrument_type)
    DO UPDATE SET dataset             = EXCLUDED.dataset,
                  active              = EXCLUDED.active,
                  available_since     = EXCLUDED.available_since,
                  price_increment     = EXCLUDED.price_increment,
                  amount_increment    = EXCLUDED.amount_increment,
                  min_trade_amount    = EXCLUDED.min_trade_amount,
                  maker_fee           = EXCLUDED.maker_fee,
                  taker_fee           = EXCLUDED.taker_fee,
                  margin              = EXCLUDED.margin,
                  inverse             = EXCLUDED.inverse,
                  contract_multiplier = EXCLUDED.contract_multiplier,
                  listing             = EXCLUDED.listing,
                  expiration          = EXCLUDED.expiration;
