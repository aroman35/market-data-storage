INSERT INTO md.trades (ts, instrument_id, side, price, quantity, trade_id)
SELECT r.ts, r.instrument_id, r.side, r.price, r.quantity, r.trade_id
FROM trades_raw r
ON CONFLICT (instrument_id, ts, trade_id) DO NOTHING;
