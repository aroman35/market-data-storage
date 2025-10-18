#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

if [[ "${UFW_ENABLE:-false}" != "true" ]]; then
  echo "UFW_ENABLE=false -> skipping UFW config"
  exit 0
fi

apt-get -y install ufw
ufw --force enable

for cidr in ${UFW_PG_CIDRS}; do
  ufw allow from "${cidr}" to any port 5432 proto tcp
done
for cidr in ${UFW_EXP_CIDRS}; do
  ufw allow from "${cidr}" to any port 9187 proto tcp
done

ufw status verbose || true
