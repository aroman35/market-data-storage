#!/usr/bin/env python3
import argparse, os, sys, json, time
from typing import Dict, Any, List
import requests
import psycopg

def slug_for_exchange(name: str) -> str:
    m = {
        "BinanceFutures": "binance-futures",
        "BinanceSpot": "binance-spot",
        "OkexFutures": "okex-futures",
        "OkexSpot": "okex-spot",
        "KucoinFutures": "kucoin-futures",
        "KucoinSpot": "kucoin-spot",
    }
    return m.get(name, name.lower())

def norm_type(t: str) -> str:
    t = (t or "").strip().lower()
    if t in ("perpetual","spot","future"):
        return t
    if t in ("swap","perp"): return "perpetual"
    return t

def main():
    p = argparse.ArgumentParser("Refresh instruments from Tardis to Postgres")
    p.add_argument("--exchange", required=True, help="Exchange as stored in DB (e.g., BinanceFutures)")
    p.add_argument("--api-key", default=os.getenv("TARDIS_API_KEY"))
    p.add_argument("--api-base", default="https://api.tardis.dev")
    p.add_argument("--dsn", required=True)
    args = p.parse_args()

    if not args.api_key:
        print("ERROR: Provide --api-key or set TARDIS_API_KEY", file=sys.stderr)
        sys.exit(1)

    slug = slug_for_exchange(args.exchange)
    url = f"{args.api_base}/v1/instruments/{slug}"
    print(f"| INFO  | Fetching instruments: {url}")
    r = requests.get(url, headers={"Authorization": f"Bearer {args.api_key}"}, timeout=60)
    r.raise_for_status()
    data = r.json()
    print(f"| INFO  | Instruments fetched: {len(data)}")

    with psycopg.connect(args.dsn, autocommit=False) as conn:
        with conn.cursor() as cur:
            # ensure exchange
            cur.execute("""
                INSERT INTO mkt.exchanges(name)
                VALUES (%s)
                ON CONFLICT (name) DO NOTHING;
            """, (args.exchange,))

            # cache exchange_id
            cur.execute("SELECT id FROM mkt.exchanges WHERE name=%s;", (args.exchange,))
            exchange_id = cur.fetchone()[0]

            # assets cache
            asset_id_cache: Dict[str, str] = {}

            def ensure_asset(name: str) -> str:
                if name in asset_id_cache:
                    return asset_id_cache[name]
                cur.execute("INSERT INTO mkt.assets(name) VALUES (%s) ON CONFLICT (name) DO NOTHING;", (name,))
                cur.execute("SELECT id FROM mkt.assets WHERE name=%s;", (name,))
                aid = cur.fetchone()[0]
                asset_id_cache[name] = aid
                return aid

            # symbols cache
            symbol_id_cache: Dict[tuple, str] = {}

            def ensure_symbol(base: str, quote: str) -> str:
                key = (base, quote)
                if key in symbol_id_cache:
                    return symbol_id_cache[key]
                base_id = ensure_asset(base)
                quote_id = ensure_asset(quote)
                cur.execute("""
                    INSERT INTO mkt.symbols(base_asset_id, quote_asset_id)
                    VALUES (%s, %s)
                    ON CONFLICT (base_asset_id, quote_asset_id) DO NOTHING;
                """,(base_id, quote_id))
                cur.execute("""
                    SELECT id FROM mkt.symbols
                    WHERE base_asset_id=%s AND quote_asset_id=%s;
                """,(base_id, quote_id))
                sid = cur.fetchone()[0]
                symbol_id_cache[key] = sid
                return sid

            # upsert instruments
            upsert_count = 0
            for it in data:
                dataset_id = (it.get("datasetId") or "").upper()
                base = (it.get("baseCurrency") or "").upper()
                quote = (it.get("quoteCurrency") or "").upper()
                itype = norm_type(it.get("type"))
                if not dataset_id or not base or not quote or itype not in ("perpetual","spot","future"):
                    continue

                symbol_id = ensure_symbol(base, quote)

                cur.execute("""
                    INSERT INTO mkt.instruments(
                        exchange_id, symbol_id, instrument_type, dataset_id,
                        active, available_since,
                        price_increment, amount_increment, min_trade_amount,
                        maker_fee, taker_fee, margin, inverse, contract_multiplier,
                        listing, expiry
                    )
                    VALUES(
                        %s, %s, %s, %s,
                        COALESCE(%s,true), %s,
                        COALESCE(%s,0), COALESCE(%s,0), COALESCE(%s,0),
                        COALESCE(%s,0), COALESCE(%s,0), %s, %s, %s,
                        %s, %s
                    )
                    ON CONFLICT (exchange_id, instrument_type, dataset_id)
                    DO UPDATE SET
                        symbol_id = EXCLUDED.symbol_id,
                        active = EXCLUDED.active,
                        available_since = EXCLUDED.available_since,
                        price_increment = EXCLUDED.price_increment,
                        amount_increment = EXCLUDED.amount_increment,
                        min_trade_amount = EXCLUDED.min_trade_amount,
                        maker_fee = EXCLUDED.maker_fee,
                        taker_fee = EXCLUDED.taker_fee,
                        margin = EXCLUDED.margin,
                        inverse = EXCLUDED.inverse,
                        contract_multiplier = EXCLUDED.contract_multiplier,
                        listing = EXCLUDED.listing,
                        expiry = EXCLUDED.expiry;
                """, (
                    exchange_id, symbol_id, itype, dataset_id,
                    it.get("active"), it.get("availableSince"),
                    it.get("priceIncrement"), it.get("amountIncrement"), it.get("minTradeAmount"),
                    it.get("makerFee"), it.get("takerFee"), it.get("margin"), it.get("inverse"), it.get("contractMultiplier"),
                    it.get("listing"), it.get("expiry")
                ))
                upsert_count += 1

            conn.commit()
            print(f"| INFO  | Instruments upserted: {upsert_count}")

if __name__ == "__main__":
    main()
