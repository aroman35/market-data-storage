crontab -e

TZ=Europe/Moscow
0 6 * * * TARDIS_API_KEY="YOUR_KEY" /usr/bin/python3 /opt/md/tardis_bulk_download.py \
  --exchange BinanceFutures \
  --type Perpetual \
  --date $(date -d "yesterday" +\%F) \
  --feeds trades l2 \
  --out-dir /data/md-cache >> /var/log/tardis_bulk_download.log 2>&1
