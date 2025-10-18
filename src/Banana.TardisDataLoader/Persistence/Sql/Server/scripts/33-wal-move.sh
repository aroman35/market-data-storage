#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

if [[ "${MOVE_WAL:-false}" != "true" ]]; then
  echo "MOVE_WAL=false -> skipping WAL relocation"
  exit 0
fi

install -d -o postgres -g postgres -m 700 "${WAL_DIR}"
systemctl stop postgresql@${PG_VERSION}-main
sudo -u postgres rsync -a "${MOUNT_POINT}/17/main/pg_wal/" "${WAL_DIR}/"
sudo -u postgres mv "${MOUNT_POINT}/17/main/pg_wal" "${MOUNT_POINT}/17/main/pg_wal.bak" || true
sudo -u postgres ln -s "${WAL_DIR}" "${MOUNT_POINT}/17/main/pg_wal"
systemctl start postgresql@${PG_VERSION}-main
sudo -u postgres psql -tAc "SELECT pg_current_wal_lsn();" >/dev/null
sudo -u postgres rm -rf "${MOUNT_POINT}/17/main/pg_wal.bak" || true
