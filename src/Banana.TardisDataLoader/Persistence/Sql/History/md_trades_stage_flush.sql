INSERT INTO md.trades (ts, instrument_id, side, price, quantity, trade_id)
SELECT ts, instrument_id, side, price, quantity, trade_id
FROM trades_stage
ORDER BY instrument_id, ts
ON CONFLICT (instrument_id, ts, trade_id) DO NOTHING;
