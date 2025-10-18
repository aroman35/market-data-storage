#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

install -d /usr/share/postgresql-common/pgdg
wget -qO- https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.gpg
. /etc/os-release
echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.gpg] http://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" > /etc/apt/sources.list.d/pgdg.list

curl -s https://packagecloud.io/install/repositories/timescale/timescaledb/script.deb.sh | bash

apt-get update -y
apt-get install -y postgresql-${PG_VERSION} postgresql-client-${PG_VERSION} timescaledb-2-postgresql-${PG_VERSION} timescaledb-tools
