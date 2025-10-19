#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="/opt/md"
VENV_DIR="${PROJECT_DIR}/.venv"
PY="${VENV_DIR}/bin/python"
OUT_DIR="/data/tardis-cache"

export TARDIS_API_KEY="${TARDIS_API_KEY:-YOUR_API_KEY_HERE}"

EXCHANGE="BinanceFutures"
INSTR_TYPE="Perpetual"
FEEDS=(trades l2)
DAY="$(date -d "yesterday" +"%Y-%m-%d")"

cd "$PROJECT_DIR"

exec "$PY" "$PROJECT_DIR/tardis_bulk_download.py" \
  --exchange "$EXCHANGE" \
  --type "$INSTR_TYPE" \
  --date "$DAY" \
  --feeds "${FEEDS[@]}" \
  --out-dir "$OUT_DIR" \
  --api-key "$TARDIS_API_KEY" \
  --workers 16 \
  --timeout 600
