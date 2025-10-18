#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
./00-prereqs.sh
./10-raid.sh
./20-mount.sh
./30-postgres-install.sh
./31-timescaledb-install.sh
./32-pg-cluster.sh
./33-wal-move.sh || true
./40-exporter.sh
./50-firewall.sh || true
./60-raid-monitoring.sh || true
./70-iotuning.sh || true
