#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

sudo -u postgres psql <<SQL
DO $$
BEGIN
   IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='${EXPORTER_USER}') THEN
      CREATE USER ${EXPORTER_USER} WITH ENCRYPTED PASSWORD '${EXPORTER_PASSWORD}';
   END IF;
END$$;
GRANT pg_monitor TO ${EXPORTER_USER};
GRANT pg_read_all_settings TO ${EXPORTER_USER};
GRANT pg_stat_scan_tables TO ${EXPORTER_USER};
GRANT CONNECT ON DATABASE postgres TO ${EXPORTER_USER};
SQL

useradd -r -s /usr/sbin/nologin -d /var/lib/postgres_exporter postgres_exporter 2>/dev/null || true
install -d -o postgres_exporter -g postgres_exporter /var/lib/postgres_exporter /etc/postgres_exporter

cat > /etc/systemd/system/postgres_exporter.service <<EOF
[Unit]
Description=Prometheus PostgreSQL exporter
After=network-online.target postgresql@${PG_VERSION}-main.service
Wants=network-online.target

[Service]
User=postgres_exporter
Group=postgres_exporter
Environment=DATA_SOURCE_NAME=postgresql://${EXPORTER_USER}:${EXPORTER_PASSWORD}@127.0.0.1:5432/postgres?sslmode=disable
ExecStart=/usr/local/bin/postgres_exporter --web.listen-address=${EXPORTER_LISTEN_ADDR} --auto-discover-databases --log.level=info
Restart=on-failure
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ProtectHome=true
MemoryDenyWriteExecute=true
LockPersonality=true

[Install]
WantedBy=multi-user.target
EOF

if ! command -v postgres_exporter >/dev/null 2>&1; then
  apt-get -y install jq curl
  ver=$(curl -s https://api.github.com/repos/prometheus-community/postgres_exporter/releases/latest | jq -r '.tag_name')
  curl -sL -o /tmp/postgres_exporter.tar.gz \
    "https://github.com/prometheus-community/postgres_exporter/releases/download/${ver}/postgres_exporter-${ver#v}.linux-amd64.tar.gz"
  tar -xzf /tmp/postgres_exporter.tar.gz -C /usr/local/bin --strip-components=1 \
    "postgres_exporter-${ver#v}.linux-amd64/postgres_exporter"
  chown root:root /usr/local/bin/postgres_exporter
fi

systemctl daemon-reload
systemctl enable --now postgres_exporter
