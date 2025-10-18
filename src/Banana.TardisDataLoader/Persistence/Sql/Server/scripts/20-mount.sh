#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

PART="${ARRAY_DEV}p1"
MP="${MOUNT_POINT:-/srv/pgdata}"
mkdir -p "$MP"

mountpoint -q "$MP" || mount "$PART" "$MP"

UUID=$(blkid -s UUID -o value "$PART")
LINE="UUID=${UUID} ${MP} xfs defaults,noatime,lazytime,logbufs=8,logbsize=256k,x-systemd.automount,x-systemd.device-timeout=30,nofail 0 0"

if ! grep -q " ${MP} " /etc/fstab; then
  echo "$LINE" >> /etc/fstab
else
  sed -i "s#^.*[[:space:]]${MP}[[:space:]].*$#${LINE}#g" /etc/fstab
fi

mdadm --detail --scan >> /etc/mdadm/mdadm.conf || true
update-initramfs -u

mkdir -p /etc/systemd/system/postgresql@17-main.service.d
cat >/etc/systemd/system/postgresql@17-main.service.d/override.conf <<'EOF'
[Unit]
RequiresMountsFor=/srv/pgdata
After=local-fs.target
EOF

systemctl daemon-reload
