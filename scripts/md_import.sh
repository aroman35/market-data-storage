#!/usr/bin/env bash
set -euo pipefail

# ---------------------------
# Config / Args
# ---------------------------
usage() {
  cat <<'USAGE'
Usage:
  md_import.sh \
    --pg-dsn "postgres://user:pass@host:5432/db" \
    --exchange BinanceFutures \
    --type perpetual \
    --cache-dir /data/md-cache \
    --date 2025-10-17
  OR
  md_import.sh ... --start-date 2025-10-01 --end-date 2025-10-17

Notes:
- instrument_type values in DB: [perpetual, spot, future]
- Cache layout: /data/md-cache/<exchange-slug>/<type>/<DATASETID>/<lower_dataset>_<YYYY-MM-DD>_{trades|l2}.csv.gz
USAGE
}

PG_DSN=""
EXCHANGE=""
TYPE=""
CACHE_DIR=""
DATE_SINGLE=""
DATE_START=""
DATE_END=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --pg-dsn)       PG_DSN="$2"; shift 2;;
    --exchange)     EXCHANGE="$2"; shift 2;;
    --type)         TYPE="$2"; shift 2;;
    --cache-dir)    CACHE_DIR="$2"; shift 2;;
    --date)         DATE_SINGLE="$2"; shift 2;;
    --start-date)   DATE_START="$2"; shift 2;;
    --end-date)     DATE_END="$2"; shift 2;;
    -h|--help)      usage; exit 0;;
    *) echo "Unknown arg: $1"; usage; exit 1;;
  esac
done

if [[ -z "$PG_DSN" || -z "$EXCHANGE" || -z "$TYPE" || -z "$CACHE_DIR" ]]; then
  echo "Missing required args"; usage; exit 1
fi

if [[ -n "$DATE_SINGLE" && (-n "$DATE_START" || -n "$DATE_END") ]]; then
  echo "Use either --date OR --start-date/--end-date"; exit 1
fi
if [[ -z "$DATE_SINGLE" && (-z "$DATE_START" || -z "$DATE_END") ]]; then
  echo "Provide --date OR both --start-date and --end-date"; exit 1
fi

# ---------------------------
# Helpers
# ---------------------------
slug_for_exchange() {
  # тот же слаг, что в загрузчике кеша
  case "$1" in
    BinanceFutures) echo "binance-futures" ;;
    BinanceSpot)    echo "binance-spot" ;;
    OkexFutures|OKXFutures|OkxFutures) echo "okx-futures" ;;
    OkexSpot|OKXSpot|OkxSpot) echo "okx-spot" ;;
    KucoinFutures)  echo "kucoin-futures" ;;
    KucoinSpot)     echo "kucoin-spot" ;;
    *) echo "$(echo "$1" | tr '[:upper:]' '[:lower:]' | tr '_' '-')" ;;
  esac
}

# DATE loop builder
build_dates() {
  if [[ -n "$DATE_SINGLE" ]]; then
    echo "$DATE_SINGLE"
  else
    # inclusive range
    python3 - "$DATE_START" "$DATE_END" <<'PY'
import sys, datetime as dt
start = dt.date.fromisoformat(sys.argv[1])
end   = dt.date.fromisoformat(sys.argv[2])
d = start
while d <= end:
    print(d.isoformat())
    d += dt.timedelta(days=1)
PY
  fi
}

# Pretty logger
log() { echo "| $1 | $2"; }

# ---------------------------
# Load DATASET -> instrument_id map
# ---------------------------
declare -A DATASET_TO_IID

load_dataset_map() {
  local exch="$1" typ="$2"
  local sql="
SELECT upper(i.dataset) AS ds, i.id::text
FROM mkt.instruments i
JOIN mkt.symbols s ON s.id=i.symbol_id
JOIN mkt.exchanges e ON e.id=s.exchange_id
WHERE e.name = '$exch' AND i.instrument_type = '$typ';
"
  # Важно: таб как разделитель колонок
  local count=0
  while IFS=$'\t' read -r DS IID; do
    [[ -z "${DS:-}" || -z "${IID:-}" ]] && continue
    DATASET_TO_IID["$DS"]="$IID"
    ((count++)) || true
  done < <(psql "$PG_DSN" -At -F $'\t' -c "$sql")
  log "INFO " "Dataset mapping loaded: $count items"
}

# ---------------------------
# Import: TRADES
# ---------------------------
import_trades_file() {
  local iid="$1"
  local fpath="$2"

  # temp staging table (session-local)
  psql "$PG_DSN" -v ON_ERROR_STOP=1 -c "
CREATE TEMP TABLE IF NOT EXISTS _tr_stage_raw (
  exchange text,
  symbol text,
  ts_micro bigint,
  local_ts_micro bigint,
  trade_id bigint,
  side text,
  price numeric,
  amount numeric
) ON COMMIT DROP;
TRUNCATE _tr_stage_raw;
"

  # \copy from gzip program; CSV HEADER
  psql "$PG_DSN" -v ON_ERROR_STOP=1 -c "\
\\copy _tr_stage_raw (exchange, symbol, ts_micro, local_ts_micro, trade_id, side, price, amount) \
FROM PROGRAM 'gzip -cd \"${fpath}\"' WITH (FORMAT csv, HEADER true)
"

  # Insert → md.trades with proper conversions
  psql "$PG_DSN" -v ON_ERROR_STOP=1 -c "
INSERT INTO md.trades (ts, instrument_id, side, price, quantity, trade_id)
SELECT
  (timestamp 'epoch' + (ts_micro::numeric * interval '1 microsecond')) AT TIME ZONE 'UTC' AS ts_utc,
  '$iid'::uuid,
  CASE lower(side)
    WHEN 'buy'  THEN 1
    WHEN 'sell' THEN 2
    ELSE NULL
  END::smallint,
  price,
  amount,
  trade_id
FROM _tr_stage_raw
WHERE ts_micro IS NOT NULL;
"
}

# ---------------------------
# Import: L2 (Level Updates)
# ---------------------------
import_l2_file() {
  local iid="$1"
  local fpath="$2"

  # temp staging
  psql "$PG_DSN" -v ON_ERROR_STOP=1 -c "
CREATE TEMP TABLE IF NOT EXISTS _l2_stage_raw (
  exchange text,
  symbol text,
  ts_micro bigint,
  local_ts_micro bigint,
  is_snapshot boolean,
  side text,
  price numeric,
  amount numeric
) ON COMMIT DROP;
TRUNCATE _l2_stage_raw;
"

  psql "$PG_DSN" -v ON_ERROR_STOP=1 -c "\
\\copy _l2_stage_raw (exchange, symbol, ts_micro, local_ts_micro, is_snapshot, side, price, amount) \
FROM PROGRAM 'gzip -cd \"${fpath}\"' WITH (FORMAT csv, HEADER true)
"

  # row_number() даст update_seq В РАМКАХ одного файла.
  # Если нужен глобальный непрерывный seq в сутки — можно добавлять оффсет из MAX(update_seq)+1 за день.
  psql "$PG_DSN" -v ON_ERROR_STOP=1 -c "
WITH ordered AS (
  SELECT
    (timestamp 'epoch' + (ts_micro::numeric * interval '1 microsecond')) AT TIME ZONE 'UTC' AS ts_utc,
    is_snapshot,
    CASE lower(side)
      WHEN 'bid' THEN 1
      WHEN 'ask' THEN 2
      ELSE NULL
    END::smallint AS side_code,
    price,
    amount,
    row_number() OVER (ORDER BY ts_micro, local_ts_micro, price, amount) AS seq
  FROM _l2_stage_raw
  WHERE ts_micro IS NOT NULL
)
INSERT INTO md.level_updates (ts, instrument_id, side, price, quantity, is_snapshot, update_seq)
SELECT ts_utc, '$iid'::uuid, side_code, price, amount, is_snapshot, seq
FROM ordered;
"
}

# ---------------------------
# Main
# ---------------------------
main() {
  local slug; slug="$(slug_for_exchange "$EXCHANGE")"

  log "INFO " "=== START IMPORT ==="
  if [[ -n "$DATE_SINGLE" ]]; then
    log "INFO " "Exchange: $EXCHANGE (slug: $slug) | Type: $TYPE | Date: $DATE_SINGLE"
  else
    log "INFO " "Exchange: $EXCHANGE (slug: $slug) | Type: $TYPE | Dates: $DATE_START..$DATE_END"
  fi
  log "INFO " "Cache dir: $CACHE_DIR"

  load_dataset_map "$EXCHANGE" "$TYPE"

  # собираем список файлов за диапазон
  mapfile -t DAYS < <(build_dates)
  declare -a FILES=()
  for d in "${DAYS[@]}"; do
    # trades
    while IFS= read -r -d '' f; do FILES+=("$f"); done < <(find "$CACHE_DIR/$slug/$TYPE" -type f -name "*_${d}_trades.csv.gz" -print0 2>/dev/null || true)
    # l2
    while IFS= read -r -d '' f; do FILES+=("$f"); done < <(find "$CACHE_DIR/$slug/$TYPE" -type f -name "*_${d}_l2.csv.gz" -print0 2>/dev/null || true)
  done

  log "INFO " "Files to import: ${#FILES[@]}"

  # импортим по одному (можно распараллелить xargs -P если нужно)
  for f in "${FILES[@]}"; do
    # datasetId — это ИМЯ ПАПКИ верхнего уровня (UPPER CASE)
    # пример: /data/md-cache/binance-futures/perpetual/DOGEUSDC/dogeusdc_2025-10-17_l2.csv.gz
    DATASET="$(basename "$(dirname "$f")")"        # DOGEUSDC
    DATASET_UP="$(echo "$DATASET" | tr '[:lower:]' '[:upper:]')"
    IID="${DATASET_TO_IID[$DATASET_UP]:-}"

    if [[ -z "${IID:-}" ]]; then
      log "WARN " "No instrument_id for dataset=$DATASET_UP (file: $f) — skip"
      continue
    fi

    if [[ "$f" == *_trades.csv.gz ]]; then
      log "INFO " "TRADES  -> $f"
      import_trades_file "$IID" "$f"
    elif [[ "$f" == *_l2.csv.gz ]]; then
      log "INFO " "L2      -> $f"
      import_l2_file "$IID" "$f"
    else
      log "WARN " "Unknown feed in filename: $f"
    fi
  done

  log "INFO " "=== IMPORT DONE ==="
}

main
