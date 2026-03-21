#!/usr/bin/env python3
"""
Extend the Grounded database with orders up to a target end date.

Adds new orders (and items) for existing customers/products covering
the gap between the current max order_date and TARGET_END.

Usage:
    python scripts/seed-extend.py [--dsn "Host=..."] [--end 2026-03-21]
"""

import argparse
import random
import sys
from datetime import datetime, timedelta, timezone, date

try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    sys.exit("psycopg2-binary not installed. Run: pip install psycopg2-binary")

SEED = 99
random.seed(SEED)

STATUSES      = [('Completed', 0.90), ('Cancelled', 0.06), ('Refunded', 0.04)]
SALES_CHANNELS= [('Web', 0.58), ('Mobile', 0.27), ('Marketplace', 0.15)]
SHIP_REGIONS  = [('West', 0.32), ('East', 0.27), ('South', 0.23), ('Central', 0.18)]

CATEGORIES = {
    'Electronics':  {'weight': 0.34},
    'Home':         {'weight': 0.24},
    'Office':       {'weight': 0.18},
    'Fitness':      {'weight': 0.14},
    'Accessories':  {'weight': 0.10},
}


def weighted_choice(choices):
    values, weights = zip(*choices)
    return random.choices(values, weights=weights, k=1)[0]


def rand_dt(start: datetime, end: datetime) -> datetime:
    delta = int((end - start).total_seconds())
    if delta <= 0:
        return start
    return start + timedelta(seconds=random.randint(0, delta))


def seasonal_weight(dt: datetime, category: str) -> float:
    m = dt.month
    if m in (11, 12):
        return 1.6
    if category == 'Fitness' and m == 1:
        return 1.4
    if category in ('Home', 'Accessories') and m == 2:
        return 0.7
    return 1.0


def parse_dsn(dsn_str: str) -> dict:
    mapping = {'host': 'Host', 'port': 'Port', 'dbname': 'Database',
               'user': 'Username', 'password': 'Password'}
    parts = {k.strip(): v.strip() for k, v in
             (pair.split('=', 1) for pair in dsn_str.split(';') if '=' in pair)}
    return {k: parts[v] for k, v in mapping.items() if v in parts}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dsn', default='Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=change_me')
    parser.add_argument('--end', default=date.today().isoformat(), help='Target end date YYYY-MM-DD (inclusive)')
    args = parser.parse_args()

    target_end = datetime.strptime(args.end, '%Y-%m-%d').replace(
        hour=23, minute=59, second=59, tzinfo=timezone.utc)

    conn = psycopg2.connect(**parse_dsn(args.dsn))
    conn.autocommit = False
    cur = conn.cursor()

    # Find current max order date
    cur.execute("SELECT MAX(order_date) FROM orders")
    current_max = cur.fetchone()[0]
    if current_max is None:
        sys.exit("No orders found — run seed.py first.")

    # Make timezone-aware
    if current_max.tzinfo is None:
        current_max = current_max.replace(tzinfo=timezone.utc)

    extend_start = current_max + timedelta(seconds=1)
    if extend_start >= target_end:
        print(f"Data already extends to {current_max.date()} — nothing to do.")
        conn.close()
        return

    days_to_add = (target_end.date() - current_max.date()).days
    print(f"Extending from {current_max.date()} → {target_end.date()} ({days_to_add} days)")

    # Load existing customers and products
    cur.execute("SELECT id FROM customers ORDER BY id")
    customer_ids = [r[0] for r in cur.fetchall()]

    cur.execute("SELECT id, category, unit_cost FROM products WHERE is_active = TRUE ORDER BY id")
    products = [(r[0], r[1], r[2]) for r in cur.fetchall()]
    prod_lookup = {p[0]: (p[1], p[2]) for p in products}
    product_ids = [p[0] for p in products]

    # Scale order rate proportionally to original density
    # Original: 36,000 orders over 2 years = ~49 orders/day
    orders_per_day = 49
    n_new_orders = max(1, round(orders_per_day * days_to_add))
    print(f"Generating ~{n_new_orders} new orders …")

    orders = []
    items  = []

    for _ in range(n_new_orders):
        order_date = rand_dt(extend_start, target_end)
        cust_id    = random.choice(customer_ids)
        status     = weighted_choice(STATUSES)
        channel    = weighted_choice(SALES_CHANNELS)
        ship_reg   = weighted_choice(SHIP_REGIONS)
        orders.append((cust_id, order_date, status, channel, ship_reg))

    # Need to know the current max order_id so items get correct order_ids
    cur.execute("SELECT MAX(id) FROM orders")
    max_order_id = cur.fetchone()[0] or 0

    print(f"Inserting {len(orders)} orders …")
    # Use fetch=True to collect all returned IDs across all internal pages
    rows = psycopg2.extras.execute_values(
        cur,
        "INSERT INTO orders (customer_id, order_date, status, sales_channel, shipping_region) VALUES %s RETURNING id",
        orders, page_size=500, fetch=True
    )
    new_order_ids = [r[0] for r in rows]

    for order_id, (cust_id, order_date, status, channel, ship_reg) in zip(new_order_ids, orders):
        n_items = random.choices([1,2,3,4,5], weights=[5,25,35,25,10], k=1)[0]
        chosen = random.sample(product_ids, min(n_items, len(product_ids)))
        for prod_id in chosen:
            cat, unit_cost = prod_lookup[prod_id]
            margin     = random.uniform(0.25, 1.5)
            unit_price = round(float(unit_cost) * (1 + margin) * (0.9 + 0.2 * random.random()), 2)
            qty        = random.choices([1,2,3,4], weights=[55,25,12,8], k=1)[0]
            discount   = 0.0
            if random.random() < 0.18:
                discount = round(random.uniform(0.01, 0.20) * qty * unit_price, 2)
            items.append((order_id, prod_id, qty, unit_price, discount))

    print(f"Inserting {len(items)} order_items …")
    psycopg2.extras.execute_values(
        cur,
        "INSERT INTO order_items (order_id, product_id, quantity, unit_price, discount_amount) VALUES %s",
        items, page_size=2000
    )

    conn.commit()
    cur.close()
    conn.close()
    print(f"Done. Added {len(orders)} orders, {len(items)} items through {target_end.date()}.")


if __name__ == '__main__':
    main()
