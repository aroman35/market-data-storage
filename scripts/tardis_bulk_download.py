#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import sys
import json
import asyncio
import aiohttp
import argparse
from pathlib import Path
from datetime import datetime, timezone, date, timedelta
from typing import List, Dict, Any, Optional, Tuple, Iterable
from tqdm.asyncio import tqdm_asyncio

# ---------- constants & helpers ----------

EXCHANGE_SLUGS = {
    "binancefutures": "binance-futures",
    "binancespot": "binance",
    "okex": "okex",
    "okexfutures": "okex-futures",
    "okexswap": "okex-swap",
    "kucoinfutures": "kucoin-futures",
    "kucoinspot": "kucoin",
}

TYPE_SLUGS = {
    "perpetual": "perpetual",
    "spot": "spot",
    "future": "future",
}

FEED_PATH = {
    "trades": "trades",
    "l2": "incremental_book_L2",
}

def to_slug_exchange(exchange: str) -> str:
    key = exchange.strip().lower()
    return EXCHANGE_SLUGS.get(key, key)

def to_slug_type(instr_type: str) -> str:
    key = instr_type.strip().lower()
    return TYPE_SLUGS.get(key, key)

def ensure_dir(p: Path) -> None:
    p.mkdir(parents=True, exist_ok=True)

def _to_utc_date(iso_str: Optional[str]) -> Optional[date]:
    """парсит ISO (с Z/офсетом или без) и возвращает календарный день в UTC."""
    if not iso_str:
        return None
    s = iso_str.replace("Z", "+00:00")
    try:
        dt = datetime.fromisoformat(s)
    except Exception:
        return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    else:
        dt = dt.astimezone(timezone.utc)
    return dt.date()

def pick(v: Dict[str, Any], *keys: str) -> Optional[Any]:
    for k in keys:
        if k in v and v[k] not in (None, ""):
            return v[k]
    return None

def get_dataset_id(d: Dict[str, Any]) -> Optional[str]:
    # datasetId/dataset/dataset_id/иначе symbol
    ds = pick(d, "datasetId", "dataset", "dataset_id")
    if ds:
        return str(ds).strip()
    sym = pick(d, "symbol", "symbolId", "symbol_id", "id")
    return str(sym).strip() if sym else None

def get_symbol_lower(d: Dict[str, Any]) -> str:
    sym = pick(d, "symbol", "symbolId", "symbol_id", "id", "datasetId", "dataset")
    return str(sym).strip().lower() if sym else "unknown"

# ---------- API calls ----------

async def fetch_instruments(session: aiohttp.ClientSession, api_base: str, exchange_slug: str) -> List[Dict[str, Any]]:
    url = f"{api_base.rstrip('/')}/v1/instruments/{exchange_slug}"
    print(f"| INFO  | Fetching instruments: {url}")
    async with session.get(url) as resp:
        resp.raise_for_status()
        text = await resp.text()
        try:
            data = json.loads(text)
        except json.JSONDecodeError:
            print(f"| ERROR | Can't parse instruments JSON (length={len(text)})", file=sys.stderr)
            raise
        if not isinstance(data, list):
            print("| ERROR | Instruments response is not a list", file=sys.stderr)
            raise RuntimeError("Bad instruments payload")
        print(f"| INFO  | Instruments fetched: {len(data)}")
        return data

def filter_instruments(items: List[Dict[str, Any]], instr_type_slug: str, day_utc: date) -> List[Dict[str, Any]]:
    """оставляем те, у кого type совпадает и day ∈ [since, to) (или без ограничений)."""
    result = []
    for it in items:
        t = str(pick(it, "type", "instrumentType") or "").strip().lower()
        if t != instr_type_slug:
            continue

        since_d = _to_utc_date(pick(it, "availableSince", "available_from"))
        to_d    = _to_utc_date(pick(it, "availableTo", "available_to"))

        if since_d and day_utc < since_d:
            continue
        if to_d and day_utc >= to_d:
            continue

        ds = get_dataset_id(it)
        if not ds:
            continue

        result.append(it)
    return result

async def download_one(
    session: aiohttp.ClientSession,
    datasets_base: str,
    exchange_slug: str,
    day: date,
    feed: str,
    instrument: Dict[str, Any],
    out_root: Path,
    type_slug: str,
    retries: int = 3,
    retry_delay: float = 5.0,
) -> Tuple[str, str, Optional[Path], Optional[str]]:
    """качаем один файл. возвращаем (datasetId, feed, saved_path|None, error|None)."""
    ds_id = get_dataset_id(instrument)  # оригинальный id для URL
    if not ds_id:
        return ("<missing-dataset>", feed, None, "no-dataset-id")

    ds_for_folder = ds_id.upper()       # имя папки — UPPERCASE
    sym_lower     = get_symbol_lower(instrument)  # для имени файла
    feed_seg      = FEED_PATH[feed]

    url = (
        f"{datasets_base.rstrip('/')}/v1/{exchange_slug}/{feed_seg}/"
        f"{day.year:04d}/{day.month:02d}/{day.day:02d}/{ds_id}.csv.gz"
    )

    # Путь: out/exchange/type/DATASETID_UPPER/xxx_YYYY-MM-DD_feed.csv.gz
    out_dir = out_root / exchange_slug / type_slug / ds_for_folder
    ensure_dir(out_dir)
    out_path = out_dir / f"{sym_lower}_{day.isoformat()}_{feed}.csv.gz"

    if out_path.exists() and out_path.stat().st_size > 0:
        return (ds_for_folder, feed, out_path, None)

    last_err = None
    for attempt in range(1, retries + 1):
        try:
            print(f"| INFO  | GET {url} [attempt {attempt}/{retries}]")
            async with session.get(url) as resp:
                if resp.status == 404:
                    return (ds_for_folder, feed, None, "404")
                if resp.status == 400:
                    txt = await resp.text()
                    return (ds_for_folder, feed, None, f"400:{txt[:200]}")
                resp.raise_for_status()
                with open(out_path, "wb") as f:
                    async for chunk in resp.content.iter_chunked(1 << 20):
                        if chunk:
                            f.write(chunk)
                print(f"| OK    | Saved: {out_path}")
                return (ds_for_folder, feed, out_path, None)
        except Exception as e:
            last_err = str(e)
            if attempt < retries:
                await asyncio.sleep(retry_delay * attempt)
    return (ds_for_folder, feed, None, last_err or "failed")

def date_range(start: date, end: date) -> Iterable[date]:
    """включительно: start..end"""
    cur = start
    while cur <= end:
        yield cur
        cur = cur + timedelta(days=1)

# ---------- main ----------

async def main():
    parser = argparse.ArgumentParser(
        description="Bulk download Tardis datasets (trades/L2) for a single day or date range."
    )
    parser.add_argument("--exchange", required=True, help="Exchange (e.g., BinanceFutures)")
    parser.add_argument("--type", required=True, help="Instrument type (Spot|Perpetual|Future)")
    # режимы дат: либо --date, либо --start-date + --end-date
    parser.add_argument("--date", help="Single UTC date YYYY-MM-DD")
    parser.add_argument("--start-date", help="Start UTC date YYYY-MM-DD (inclusive)")
    parser.add_argument("--end-date", help="End UTC date YYYY-MM-DD (inclusive)")
    parser.add_argument("--feeds", nargs="+", default=["trades", "l2"], choices=["trades", "l2"], help="Which feeds to download")
    parser.add_argument("--out-dir", required=True, help="Output directory")
    parser.add_argument("--api-key", default=os.getenv("TARDIS_API_KEY"), help="Tardis API key (or env TARDIS_API_KEY)")
    parser.add_argument("--api-base", default="https://api.tardis.dev", help="Tardis API base")
    parser.add_argument("--datasets-base", default="https://datasets.tardis.dev", help="Tardis datasets base")
    parser.add_argument("--workers", type=int, default=16, help="Parallel workers")
    parser.add_argument("--timeout", type=int, default=600, help="Per-request timeout seconds")
    args = parser.parse_args()

    # валидация дат
    if args.date and (args.start_date or args.end_date):
        print("| ERROR | Use either --date OR --start-date/--end-date.", file=sys.stderr)
        sys.exit(1)
    if not args.date and not (args.start_date and args.end_date):
        print("| ERROR | Provide --date OR both --start-date and --end-date.", file=sys.stderr)
        sys.exit(1)

    def parse_date(s: str) -> date:
        try:
            return datetime.fromisoformat(s).date()
        except ValueError:
            print(f"| ERROR | Bad date: {s}. Use YYYY-MM-DD", file=sys.stderr)
            sys.exit(1)

    if args.date:
        dates = [parse_date(args.date)]
    else:
        sd = parse_date(args.start_date)
        ed = parse_date(args.end_date)
        if ed < sd:
            print("| ERROR | --end-date must be >= --start-date", file=sys.stderr)
            sys.exit(1)
        dates = list(date_range(sd, ed))

    exchange_slug = to_slug_exchange(args.exchange)
    type_slug = to_slug_type(args.type)

    feeds: List[str] = []
    for f in args.feeds:
        key = f.strip().lower()
        feeds.append("trades" if key == "trades" else "l2")
    feeds = list(dict.fromkeys(feeds))
    if not feeds:
        print("| ERROR | No valid feeds selected", file=sys.stderr)
        sys.exit(1)

    if not args.api_key:
        print("| WARN  | No API key provided; you may hit rate-limits.")

    out_root = Path(args.out_dir).resolve()
    ensure_dir(out_root)

    print("| INFO  | === START ===")
    print(f"| INFO  | Exchange: {exchange_slug} | Type: {type_slug} | Dates: {dates[0]}..{dates[-1]} | Feeds: {', '.join(feeds)}")
    print(f"| INFO  | Out dir: {out_root} | Workers: {args.workers} | Timeout: {args.timeout}s | No-Preflight")

    timeout = aiohttp.ClientTimeout(total=args.timeout)
    headers = {"Accept": "application/json"}
    if args.api_key:
        headers["Authorization"] = f"Bearer {args.api_key}"

    connector = aiohttp.TCPConnector(limit=0)
    async with aiohttp.ClientSession(timeout=timeout, headers=headers, connector=connector) as session:
        # 1) инструменты по бирже
        instruments = await fetch_instruments(session, args.api_base, exchange_slug)

        total_done = total_ok = total_404 = total_failed = 0

        for day in dates:
            # 2) фильтр по типу и доступности на день
            filtered = filter_instruments(instruments, type_slug, day)
            print(f"| INFO  | [{day}] After type/date filter: {len(filtered)}")
            if not filtered:
                continue

            # 3) задачи
            sem = asyncio.Semaphore(args.workers)
            tasks: List[asyncio.Task] = []

            async def _wrap_download(feed: str, it: Dict[str, Any]):
                async with sem:
                    return await download_one(
                        session=session,
                        datasets_base=args.datasets_base,
                        exchange_slug=exchange_slug,
                        day=day,
                        feed=feed,
                        instrument=it,
                        out_root=out_root,
                        type_slug=type_slug,
                        retries=3,
                        retry_delay=5.0,
                    )

            for it in filtered:
                for f in feeds:
                    tasks.append(asyncio.create_task(_wrap_download(f, it)))

            # 4) прогресс
            day_done = day_ok = day_404 = day_failed = 0
            for coro in tqdm_asyncio.as_completed(tasks, total=len(tasks), desc=f"Downloading {day}", ncols=100):
                ds_folder, feed, path, err = await coro
                day_done += 1
                if path and err is None:
                    day_ok += 1
                elif err == "404":
                    day_404 += 1
                    print(f"| SKIP  | 404 (no file) {ds_folder}/{feed} @ {day.isoformat()}")
                else:
                    day_failed += 1
                    print(f"| ERR   | {ds_folder}/{feed} @ {day.isoformat()} -> {err}")

            print(f"| INFO  | [{day}] Done: {day_done} | Saved: {day_ok} | 404: {day_404} | Failed: {day_failed}")
            total_done += day_done
            total_ok   += day_ok
            total_404  += day_404
            total_failed += day_failed

        print(f"| INFO  | TOTAL Done: {total_done} | Saved: {total_ok} | 404: {total_404} | Failed: {total_failed}")

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("| WARN  | Interrupted by user")
        sys.exit(130)
