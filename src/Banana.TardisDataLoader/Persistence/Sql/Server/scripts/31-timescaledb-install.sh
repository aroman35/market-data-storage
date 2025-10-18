#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

systemctl stop postgresql@${PG_VERSION}-main || true
pg_dropcluster --stop ${PG_VERSION} main || true

mkdir -p "${MOUNT_POINT}/17/main"
chown -R postgres:postgres "${MOUNT_POINT}"
pg_createcluster ${PG_VERSION} main --datadir="${MOUNT_POINT}/17/main" -- --data-checksums

CONF_DIR="/etc/postgresql/${PG_VERSION}/main/conf.d"
mkdir -p "${CONF_DIR}"

cat > "${CONF_DIR}/00-timescaledb.conf" <<EOF
shared_preload_libraries = 'timescaledb${ENABLE_PG_STAT_STATEMENTS:+,pg_stat_statements}'
timescaledb.max_background_workers = 16
${ENABLE_PG_STAT_STATEMENTS:+pg_stat_statements.max = 10000}
${ENABLE_PG_STAT_STATEMENTS:+pg_stat_statements.track = all}
EOF

cat > "${CONF_DIR}/01-network.conf" <<EOF
listen_addresses = '${LISTEN_ADDRESSES}'
port = 5432
EOF

cat > "${CONF_DIR}/10-memory.conf" <<EOF
shared_buffers = ${SHARED_BUFFERS}
effective_cache_size = ${EFFECTIVE_CACHE_SIZE}
maintenance_work_mem = ${MAINTENANCE_WORK_MEM}
work_mem = ${WORK_MEM}
wal_compression = on
max_wal_size = ${MAX_WAL_SIZE}
checkpoint_timeout = ${CHECKPOINT_TIMEOUT}
random_page_cost = ${RANDOM_PAGE_COST}
effective_io_concurrency = ${EFFECTIVE_IO_CONCURRENCY}
EOF

cat > "${CONF_DIR}/20-logging.conf" <<'EOF'
logging_collector = on
log_directory = 'log'
log_filename = 'postgresql-%a.log'
log_rotation_age = 1d
log_rotation_size = 1GB
log_truncate_on_rotation = on
log_line_prefix = '%m [%p] %q%u@%d %r '
log_checkpoints = on
log_lock_waits = on
log_temp_files = 0
log_min_duration_statement = 500ms
EOF

echo "timescaledb.telemetry_level = 'off'" > "${CONF_DIR}/30-timescale.conf"

systemctl enable --now postgresql@${PG_VERSION}-main

sudo -u postgres psql -c "ALTER ROLE ${PG_SUPERUSER} WITH PASSWORD '${PG_SUPERUSER_PASSWORD}';"

for cidr in ${ALLOW_CIDRS}; do
  echo "host    all     all     ${cidr}    scram-sha-256" >> "/etc/postgresql/${PG_VERSION}/main/pg_hba.conf"
done

grep -q "^local\s\+all\s\+${EXPORTER_USER}\s\+scram-sha-256" "/etc/postgresql/${PG_VERSION}/main/pg_hba.conf" || \
  tee -a "/etc/postgresql/${PG_VERSION}/main/pg_hba.conf" >/dev/null <<EOF
local   all   ${EXPORTER_USER}              scram-sha-256
host    all   ${EXPORTER_USER} 127.0.0.1/32 scram-sha-256
host    all   ${EXPORTER_USER} ::1/128      scram-sha-256
EOF

systemctl reload postgresql@${PG_VERSION}-main
