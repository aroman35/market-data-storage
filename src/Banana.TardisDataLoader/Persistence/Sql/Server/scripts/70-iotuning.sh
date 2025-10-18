#!/usr/bin/env bash
set -euo pipefail

cat > /etc/udev/rules.d/60-io-scheduler.rules <<'EOF'
ACTION=="add|change", KERNEL=="md*", ATTR{queue/scheduler}="mq-deadline", ATTR{bdi/read_ahead_kb}="4096"
ACTION=="add|change", KERNEL=="nvme*", ATTR{queue/scheduler}="none", ATTR{bdi/read_ahead_kb}="128"
EOF
udevadm control --reload && udevadm trigger

cat > /etc/sysctl.d/99-pg-raid.conf <<'EOF'
vm.swappiness = 5
vm.dirty_background_ratio = 5
vm.dirty_ratio = 20
kernel.sem = 250 1024000 100 4096
EOF
sysctl --system
