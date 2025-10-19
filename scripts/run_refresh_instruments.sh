#!/usr/bin/env bash
set -euo pipefail

# === CONFIG ===
BASE_DIR="/opt/md"
VENV_DIR="$BASE_DIR/.venv"
LOG_DIR="/var/log/md"
PY="$VENV_DIR/bin/python"

EXCHANGE="${EXCHANGE:-BinanceFutures}"
PG_DSN="${PG_DSN:-postgresql://pguser:pgpass@127.0.0.1:5432/marketdata}"
TARDIS_API_KEY="${TARDIS_API_KEY:-}"
API_BASE="${API_BASE:-https://api.tardis.dev}"

mkdir -p "$LOG_DIR"

ts() { date "+%Y-%m-%d %H:%M:%S"; }

if [[ -z "$TARDIS_API_KEY" ]]; then
  echo "$(ts) | ERROR | TARDIS_API_KEY is empty. Export it before run." | tee -a "$LOG_DIR/refresh_instruments.log"
  exit 1
fi

if [[ ! -x "$PY" ]]; then
  echo "$(ts) | INFO  | Python venv not found. Creating at $VENV_DIR ..." | tee -a "$LOG_DIR/refresh_instruments.log"
  python3 -m venv "$VENV_DIR"
  "$VENV_DIR/bin/pip" install --upgrade pip >/dev/null
  "$VENV_DIR/bin/pip" install requests "psycopg[binary]" >/dev/null
fi

echo "$(ts) | INFO  | refreshing instruments for $EXCHANGE ..." | tee -a "$LOG_DIR/refresh_instruments.log"
exec "$PY" "$BASE_DIR/refresh_instruments.py" \
  --exchange "$EXCHANGE" \
  --api-key "$TARDIS_API_KEY" \
  --api-base "$API_BASE" \
  --dsn "$PG_DSN" \
  >>"$LOG_DIR/refresh_instruments.log" 2>&1
