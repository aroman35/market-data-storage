# PostgreSQL 17 + TimescaleDB на Intel RST RAID5 (Ubuntu 24.04.3)

Набор инфраструктуры-как-код (IaC) для развёртывания PostgreSQL 17 с TimescaleDB на массиве RAID5 (Intel RST / IMSM) с XFS и метриками для Prometheus. Подходит для «голой» Ubuntu 24.04.3, подключённой по SSH.

## Архитектура

```
[BIOS Intel RST]
  └─ (IMSM контейнер) md127
       └─ md126 (RAID5, chunk 128K)
            └─ md126p1 (GPT)
                 └─ XFS /srv/pgdata (su=128k, sw=3)
                      ├─ PostgreSQL 17 + TimescaleDB 2.x
                      │    ├─ conf.d в /etc/postgresql/17/main/conf.d
                      │    └─ (опц.) pg_wal → NVMe (/srv/pgwal) symlink
                      └─ postgres_exporter (порт 9187) для Prometheus
```

## Что устанавливается

- `mdadm`, `xfsprogs`, `parted`, `smartmontools`
- PostgreSQL 17 из PGDG
- TimescaleDB 2.x + `timescaledb-tools`
- Сервис **Postgres Exporter** для Prometheus (порт 9187)
- (опц.) UFW правила
- (опц.) Мониторинг RAID (mdadm e-mail, ежемесячный scrubbing) и SMART
- (опц.) I/O-тюнинг: udev-правила для планировщиков и `sysctl`

## Требования

- Ubuntu **24.04.3** LTS (amd64), `sudo`/root
- RAID5 собран в BIOS через **Intel RST** (IMSM). В Ubuntu массив виден как `md126` (RAID5) и `md127` (контейнер).
- Доступ в Интернет к репозиториям PGDG и Timescale.

## Структура проекта

```
pg-rst-iac/
├─ .env.example        # шаблон переменных окружения
├─ README.md           # этот файл
├─ scripts/
│  ├─ 00-prereqs.sh                # утилиты, mdadm, xfsprogs, initramfs
│  ├─ 10-raid.sh                   # выбор md-устройства, GPT, XFS (su/sw)
│  ├─ 20-mount.sh                  # монтирование, fstab, systemd drop-in
│  ├─ 30-postgres-install.sh       # PGDG + Timescale репозитории и пакеты
│  ├─ 31-timescaledb-install.sh    # кластер на /srv/pgdata/17/main, конфиги
│  ├─ 33-wal-move.sh               # (опц.) перенос pg_wal на NVMe
│  ├─ 40-exporter.sh               # postgres_exporter как systemd-сервис
│  ├─ 50-firewall.sh               # (опц.) UFW правила
│  ├─ 60-raid-monitoring.sh        # (опц.) mdadm алерты, скраб, smartd
│  └─ 70-iotuning.sh               # (опц.) udev + sysctl для IO/RAID
└─ configs/
   └─ postgres/
      └─ queries.yml               # примеры кастом-метрик для экспортера
```

## Быстрый старт

```bash
# 1) подготовьте .env из шаблона
cp .env.example .env
vim .env

# 2) запустите скрипты по порядку
cd scripts
chmod +x *.sh
./00-prereqs.sh
./10-raid.sh
./20-mount.sh
./30-postgres-install.sh
./31-timescaledb-install.sh
# (опционально) вынести WAL на NVMe
./33-wal-move.sh
# метрики
./40-exporter.sh
# (опционально)
./50-firewall.sh
./60-raid-monitoring.sh
./70-iotuning.sh
```

## Переменные окружения (`.env`)

| Переменная | Назначение | Пример/дефолт |
|---|---|---|
| `ARRAY_DEV` | md-устройство RAID5 (автодетект, если пусто) | `/dev/md126` |
| `MOUNT_POINT` | Точка монтирования данных | `/srv/pgdata` |
| `FS_LABEL` | Метка файловой системы | `pgdata` |
| `XFS_SU_KB` | Stripe unit (КБ) fallback | `128` |
| `XFS_SW` | Stripe width (число data-дисков) fallback | `3` |
| `PG_VERSION` | Версия Postgres | `17` |
| `PG_SUPERUSER` | Имя суперпользователя | `postgres` |
| `PG_SUPERUSER_PASSWORD` | Пароль суперпользователя | `***` |
| `LISTEN_ADDRESSES` | Интерфейсы прослушивания | `"*"` |
| `ENABLE_PG_STAT_STATEMENTS` | Включать pg_stat_statements | `true/false` |
| `SHARED_BUFFERS` | Буферы | `32GB` |
| `EFFECTIVE_CACHE_SIZE` | Эффективный cache | `96GB` |
| `WORK_MEM` | work_mem | `64MB` |
| `MAINTENANCE_WORK_MEM` | maintenance_work_mem | `2GB` |
| `MAX_WAL_SIZE` | max_wal_size | `8GB` |
| `CHECKPOINT_TIMEOUT` | checkpoint_timeout | `15min` |
| `RANDOM_PAGE_COST` | random_page_cost | `3.5` |
| `EFFECTIVE_IO_CONCURRENCY` | effective_io_concurrency | `256` |
| `MOVE_WAL` | Перенос pg_wal на NVMe | `true/false` |
| `WAL_DIR` | Каталог для WAL | `/srv/pgwal` |
| `ALLOW_CIDRS` | Списки CIDR для удалённого доступа к PG | `"10.0.1.0/24 203.0.113.10/32"` |
| `EXPORTER_USER` | Пользователь экспортера | `postgres_exporter` |
| `EXPORTER_PASSWORD` | Пароль экспортера | `***` |
| `EXPORTER_LISTEN_ADDR` | Адрес/порт экспортера | `127.0.0.1:9187` |
| `UFW_ENABLE` | Включить UFW | `true/false` |
| `UFW_PG_CIDRS` | CIDR для 5432 | `"10.0.1.0/24"` |
| `UFW_EXP_CIDRS` | CIDR для 9187 | `"203.0.113.10/32"` |
| `PGBR_*` | (опц.) pgBackRest S3/YC | см. `.env.example` |
| `ALERT_EMAIL` | (опц.) почта для mdadm | `root` |

## Детали выполнения скриптов

### 00-prereqs.sh
- Устанавливает необходимые пакеты.
- Выполняет `mdadm --assemble --scan` и `update-initramfs -u` для автосборки массивов.

### 10-raid.sh
- Определяет `ARRAY_DEV` (или использует значение из `.env`).
- Создаёт GPT и раздел `p1` на весь том (если разметки нет).
- Создаёт XFS с параметрами `su/sw`, прочитанными из `mdadm --detail` (для RAID5 `sw = N-1`).

### 20-mount.sh
- Монтирует `md126p1` в `${MOUNT_POINT}`.
- `fstab` строка из скрипта (пример):

  ```
  UUID=<...> /srv/pgdata xfs defaults,noatime,lazytime,logbufs=8,logbsize=256k,x-systemd.automount,x-systemd.device-timeout=30,nofail 0 0
  ```

- Добавляет drop-in для сервиса Postgres: 

  **/etc/systemd/system/postgresql@17-main.service.d/override.conf**
  ```ini
  [Unit]
  RequiresMountsFor=/srv/pgdata
  After=local-fs.target
  ```

### 30-postgres-install.sh
- Подключает репозитории PGDG и TimescaleDB.
- Устанавливает `postgresql-17`, `postgresql-client-17`, `timescaledb-2-postgresql-17`, `timescaledb-tools`.

### 31-timescaledb-install.sh
- Удаляет дефолтный кластер и создаёт **на /srv/pgdata/17/main** с `--data-checksums`.
- Заполняет конфиги в `/etc/postgresql/17/main/conf.d`:
  - `00-timescaledb.conf` — `shared_preload_libraries = 'timescaledb[,pg_stat_statements]'`.
  - `01-network.conf` — `listen_addresses`, `port`.
  - `10-memory.conf` — память и I/O параметры.
  - `20-logging.conf` — логирование и вращение логов.
  - `30-timescale.conf` — `timescaledb.telemetry_level='off'`.
- Устанавливает пароль суперпользователя и добавляет в `pg_hba.conf` правила из `ALLOW_CIDRS`.
- Перезапускает кластер.

### 33-wal-move.sh (опционально)
- Перносит `pg_wal` в `${WAL_DIR}` (например, NVMe) и создаёт symlink.
- Запускает кластер и удаляет резервную папку.

### 40-exporter.sh
- Создаёт роль `${EXPORTER_USER}` с минимальными правами (`pg_monitor`, `pg_read_all_settings`, `pg_stat_scan_tables`).
- Ставит systemd-юнит `postgres_exporter` с bind-адресом из `EXPORTER_LISTEN_ADDR`.
- При отсутствии бинаря скачивает последний релиз с GitHub и размещает в `/usr/local/bin/`.

### 50-firewall.sh (опционально)
- Включает UFW и открывает порты 5432/9187 только для заданных CIDR.

### 60-raid-monitoring.sh (опционально)
- Включает e-mail алерты `mdadm` на `ALERT_EMAIL`.
- Создаёт ежемесячный скраб массивов.
- Включает `smartd` с еженедельным short self-test.

### 70-iotuning.sh (опционально)
- Udev-правила для планировщиков и readahead (RAID: `mq-deadline`/4096 KB, NVMe: `none`/128 KB).
- `sysctl`: `vm.swappiness=5`, `dirty_*`, `kernel.sem` — для PostgreSQL.

## Проверка после установки

```bash
# RAID/ФС
cat /proc/mdstat
lsblk -o NAME,SIZE,TYPE,FSTYPE,MOUNTPOINT /dev/md126
xfs_info /srv/pgdata

# PostgreSQL
systemctl status postgresql@17-main
sudo -u postgres psql -tAc "SHOW data_directory; SHOW shared_preload_libraries;"
sudo -u postgres psql -tAc "SELECT current_setting('shared_buffers'), current_setting('max_wal_size');"

# Экспортер
systemctl status postgres_exporter
curl -s http://<IP_сервера>:9187/metrics | head
```

## Интеграция с Prometheus

```yaml
scrape_configs:
  - job_name: 'postgres-17'
    static_configs:
      - targets: ['<IP_сервера>:9187']
```

При необходимости привяжите экспортёр к одному IP (`EXPORTER_LISTEN_ADDR="10.0.1.20:9187"`) и откройте порт только для адреса Prometheus.

## TimescaleDB: примеры

```sql
-- БД и гипертаблица
CREATE DATABASE tsdb;
\c tsdb
CREATE TABLE metrics(
  ts timestamptz NOT NULL,
  host text NOT NULL,
  v double precision NOT NULL
);
SELECT create_hypertable('metrics','ts', chunk_time_interval => interval '1 day');

-- Политики: компрессия и ретеншн
ALTER TABLE metrics SET (timescaledb.compress, timescaledb.compress_segmentby = 'host');
SELECT add_compression_policy('metrics', INTERVAL '7 days');
SELECT add_retention_policy('metrics',   INTERVAL '90 days');

-- Непрерывный агрегат
CREATE MATERIALIZED VIEW metrics_1h
WITH (timescaledb.continuous) AS
SELECT time_bucket('1 hour', ts) AS bucket, host, avg(v) AS avg_v
FROM metrics GROUP BY 1,2;
SELECT add_continuous_aggregate_policy('metrics_1h',
  start_offset => INTERVAL '7 days',
  end_offset   => INTERVAL '1 hour',
  schedule_interval => INTERVAL '15 minutes');
```

## Поведение при перезагрузке

- Старт PostgreSQL гарантирован после готовности точки монтирования данных благодаря drop-in:
  ```ini
  [Unit]
  RequiresMountsFor=/srv/pgdata
  After=local-fs.target
  ```

- Автомонтирование точки `/srv/pgdata` управляется `fstab` строкой вида:
  ```
  UUID=<...> /srv/pgdata xfs defaults,noatime,lazytime,logbufs=8,logbsize=256k,x-systemd.automount,x-systemd.device-timeout=30,nofail 0 0
  ```

## Безопасность

- Используйте сильные пароли в `.env` (`PG_SUPERUSER_PASSWORD`, `EXPORTER_PASSWORD`).
- В `ALLOW_CIDRS` перечисляйте только доверенные подсети/хосты.
- При внешнем доступе к Postgres включайте SSL (сертификат/ключ в отдельном конфиге `conf.d`).

## Лицензия и вклад

Шаблон предназначен для адаптации под вашу среду. Изменения и доработки приветствуются в вашем репозитории с обычным code review.

---

**Контрольный чек-лист:**

- [ ] `md126p1` смонтирован в `/srv/pgdata`, `xfs_info` показывает `sunit=256`, `swidth=768`  
- [ ] `SHOW data_directory` → `/srv/pgdata/17/main`  
- [ ] `SHOW shared_preload_libraries` содержит `timescaledb`  
- [ ] Удалённые подключения разрешены для `ALLOW_CIDRS`  
- [ ] `curl http://<IP>:9187/metrics` отдаёт метрики экспортера
