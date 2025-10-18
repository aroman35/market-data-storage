#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env || true

export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y --no-install-recommends \
  mdadm xfsprogs parted lvm2 \
  jq curl wget gpg rsync \
  smartmontools ca-certificates

mdadm --assemble --scan || true
update-initramfs -u
