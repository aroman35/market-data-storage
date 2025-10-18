INSERT INTO md.level_updates (ts, instrument_id, side, price, quantity, is_snapshot, update_seq)
SELECT ts, instrument_id, side, price, quantity, is_snapshot, update_seq
FROM level_updates_stage
ORDER BY instrument_id, ts
ON CONFLICT (instrument_id, ts, update_seq) DO NOTHING;
