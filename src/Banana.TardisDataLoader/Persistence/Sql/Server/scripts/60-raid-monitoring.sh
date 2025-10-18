#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

if ! grep -q "^MAILADDR" /etc/mdadm/mdadm.conf; then
  echo "MAILADDR ${ALERT_EMAIL:-root}" >> /etc/mdadm/mdadm.conf
fi
systemctl enable --now mdmonitor 2>/dev/null || systemctl enable --now mdadm 2>/dev/null || true

cat > /etc/cron.monthly/md-check <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
for md in /sys/block/md*/md/sync_action; do echo check > "$md"; done
EOF
chmod +x /etc/cron.monthly/md-check

apt-get -y install smartmontools
cat > /etc/smartd.conf <<'EOF'
DEVICESCAN -d sat -a -o on -S on -s S/../../7/03
EOF
systemctl enable --now smartd
