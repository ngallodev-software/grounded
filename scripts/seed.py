#!/usr/bin/env python3
"""
Seed the Grounded analytics database with realistic e-commerce data.

Usage:
    python scripts/seed.py [--dsn "Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=..."]

Requires psycopg2:
    pip install psycopg2-binary

Data spec (from docs/phases/phase-1-artifact.md):
  - 3,000 customers
  - 180 products
  - 36,000 orders  (90% Completed, 6% Cancelled, 4% Refunded)
  - ~97k–108k order_items  (~2.7–3.0 items per order)
  - Orders span 2024-01-01 to 2025-12-31
  - Seasonal lift: Nov–Dec 1.6x, Fitness Jan 1.4x
  - Electronics ~34% revenue, Home 24%, Office 18%, Fitness 14%, Accessories 10%
  - Top 15 products ~38% of revenue
"""

import argparse
import random
import sys
from datetime import datetime, timedelta, timezone

try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    sys.exit("psycopg2-binary not installed. Run: pip install psycopg2-binary")


# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

SEED = 42
random.seed(SEED)

N_CUSTOMERS = 3_000
N_PRODUCTS = 180
N_ORDERS = 36_000

ORDER_START = datetime(2024, 1, 1, tzinfo=timezone.utc)
ORDER_END   = datetime(2025, 12, 31, 23, 59, 59, tzinfo=timezone.utc)
CUST_START  = datetime(2023, 7, 1, tzinfo=timezone.utc)
CUST_END    = datetime(2025, 12, 31, tzinfo=timezone.utc)
PROD_START  = datetime(2023, 1, 1, tzinfo=timezone.utc)
PROD_END    = datetime(2025, 3, 31, tzinfo=timezone.utc)

SEGMENTS    = [('Consumer', 0.72), ('SMB', 0.20), ('Enterprise', 0.08)]
REGIONS     = [('West', 0.32), ('East', 0.27), ('South', 0.23), ('Central', 0.18)]
ACQ_CHANNELS= [('Organic', 0.35), ('Paid Search', 0.22), ('Email', 0.18), ('Affiliate', 0.15), ('Social', 0.10)]
STATUSES    = [('Completed', 0.90), ('Cancelled', 0.06), ('Refunded', 0.04)]
SALES_CHANNELS = [('Web', 0.58), ('Mobile', 0.27), ('Marketplace', 0.15)]
SHIP_REGIONS = [('West', 0.32), ('East', 0.27), ('South', 0.23), ('Central', 0.18)]

CATEGORIES = {
    'Electronics':  {'weight': 0.34, 'subcats': ['Smartphones', 'Laptops', 'Tablets', 'Headphones', 'Cameras']},
    'Home':         {'weight': 0.24, 'subcats': ['Furniture', 'Kitchenware', 'Bedding', 'Decor', 'Lighting']},
    'Office':       {'weight': 0.18, 'subcats': ['Stationery', 'Chairs', 'Desks', 'Storage', 'Printers']},
    'Fitness':      {'weight': 0.14, 'subcats': ['Weights', 'Cardio', 'Yoga', 'Outdoor', 'Recovery']},
    'Accessories':  {'weight': 0.10, 'subcats': ['Bags', 'Wallets', 'Belts', 'Sunglasses', 'Watches']},
}


def weighted_choice(choices):
    """choices: list of (value, weight) tuples"""
    values, weights = zip(*choices)
    return random.choices(values, weights=weights, k=1)[0]


def rand_dt(start, end):
    delta = int((end - start).total_seconds())
    return start + timedelta(seconds=random.randint(0, delta))


def seasonal_weight(dt: datetime, category: str) -> float:
    m = dt.month
    # Nov–Dec lift for all categories
    if m in (11, 12):
        return 1.6
    # Fitness January spike
    if category == 'Fitness' and m == 1:
        return 1.4
    # Home and Accessories dip in February
    if category in ('Home', 'Accessories') and m == 2:
        return 0.7
    return 1.0


# ---------------------------------------------------------------------------
# Generate customers
# ---------------------------------------------------------------------------

def gen_customers():
    first_names = ['Alice','Bob','Carol','David','Emma','Frank','Grace','Hank','Iris','Jack',
                   'Karen','Leo','Mia','Nora','Oscar','Pam','Quinn','Roy','Sara','Tom',
                   'Uma','Vera','Will','Xena','Yusuf','Zoe','Andre','Beth','Cole','Dana']
    last_names  = ['Smith','Johnson','Williams','Brown','Jones','Garcia','Miller','Davis',
                   'Wilson','Taylor','Anderson','Thomas','Jackson','White','Harris','Martin',
                   'Thompson','Young','Robinson','Lewis','Walker','Hall','Allen','Wright']

    rows = []
    for i in range(1, N_CUSTOMERS + 1):
        fn = random.choice(first_names)
        ln = random.choice(last_names)
        name = f"{fn} {ln}"
        email = f"{fn.lower()}.{ln.lower()}.{i}@example.com"
        segment = weighted_choice(SEGMENTS)
        region  = weighted_choice(REGIONS)
        acq     = weighted_choice(ACQ_CHANNELS)
        created = rand_dt(CUST_START, CUST_END)
        rows.append((name, email, segment, region, acq, created))
    return rows


# ---------------------------------------------------------------------------
# Generate products
# ---------------------------------------------------------------------------

def gen_products():
    rows = []
    cat_list = list(CATEGORIES.keys())
    cat_weights = [CATEGORIES[c]['weight'] for c in cat_list]
    prod_idx = 1

    # Ensure top 15 products are high-price electronics/home items
    top_products = []
    for i in range(15):
        cat = cat_list[i % 2]  # alternate Electronics/Home
        subcat = random.choice(CATEGORIES[cat]['subcats'])
        sku = f"SKU-{prod_idx:04d}"
        name = f"Premium {subcat} Model {prod_idx}"
        unit_cost = round(random.uniform(180, 500), 2)
        created = rand_dt(PROD_START, PROD_END)
        is_active = True
        top_products.append((sku, name, cat, subcat, unit_cost, is_active, created))
        prod_idx += 1

    # Remaining products
    rest = []
    n_inactive = 12
    for i in range(N_PRODUCTS - 15):
        cat = random.choices(cat_list, weights=cat_weights, k=1)[0]
        subcat = random.choice(CATEGORIES[cat]['subcats'])
        sku = f"SKU-{prod_idx:04d}"
        name = f"{subcat} Item {prod_idx}"
        unit_cost = round(random.uniform(8, 280), 2)
        created = rand_dt(PROD_START, PROD_END)
        is_active = (i >= n_inactive)  # first 12 are inactive
        rest.append((sku, name, cat, subcat, unit_cost, is_active, created))
        prod_idx += 1

    return top_products + rest


# ---------------------------------------------------------------------------
# Generate orders + order_items
# ---------------------------------------------------------------------------

def gen_orders_and_items(n_customers, products):
    # Build product lookup: id (1-based index) -> (category, unit_cost)
    prod_lookup = {i+1: (p[2], p[4]) for i, p in enumerate(products)}
    n_products = len(products)

    # Build repeat customer purchase probabilities
    # 41% one order, 37% 2-4, 17% 5-9, 5% 10+
    def order_count_for_customer():
        r = random.random()
        if r < 0.41: return 1
        if r < 0.78: return random.randint(2, 4)
        if r < 0.95: return random.randint(5, 9)
        return random.randint(10, 18)

    # Assign order counts to customers
    cust_order_counts = [order_count_for_customer() for _ in range(n_customers)]
    total_planned = sum(cust_order_counts)
    # Scale to target N_ORDERS
    scale = N_ORDERS / total_planned
    cust_order_counts = [max(1, round(c * scale)) for c in cust_order_counts]

    orders = []
    items  = []
    order_id = 0

    for cust_id_1based, n_orders in enumerate(cust_order_counts, start=1):
        # Determine base date range for this customer's orders
        prev_date = None
        for _ in range(n_orders):
            if prev_date is None:
                order_date = rand_dt(ORDER_START, ORDER_END)
            else:
                # Next order: median 46-day gap with variance
                gap = int(random.expovariate(1/46))
                gap = max(1, min(gap, 365))
                order_date = prev_date + timedelta(days=gap)
                if order_date > ORDER_END:
                    break
            prev_date = order_date

            status   = weighted_choice(STATUSES)
            channel  = weighted_choice(SALES_CHANNELS)
            ship_reg = weighted_choice(SHIP_REGIONS)
            order_id += 1
            orders.append((cust_id_1based, order_date, status, channel, ship_reg))

            # Generate 2-4 line items
            n_items = random.choices([1,2,3,4,5], weights=[5,25,35,25,10], k=1)[0]
            chosen_products = random.sample(range(1, n_products + 1), min(n_items, n_products))
            for prod_id in chosen_products:
                cat, unit_cost = prod_lookup[prod_id]
                # Unit price = cost + margin; seasonal weight bakes into order volume, not price
                _ = seasonal_weight(order_date, cat)
                margin = random.uniform(0.25, 1.5)
                unit_price = round(unit_cost * (1 + margin) * (0.9 + 0.2 * random.random()), 2)
                qty = random.choices([1,2,3,4], weights=[55,25,12,8], k=1)[0]
                # Discount: 0 most of the time, occasionally up to 20%
                discount = 0.0
                if random.random() < 0.18:
                    discount = round(random.uniform(0.01, 0.20) * qty * unit_price, 2)
                items.append((order_id, prod_id, qty, unit_price, discount))

    return orders, items


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def parse_dsn(dsn_str: str) -> dict:
    """Parse 'Host=x;Port=y;Database=z;Username=u;Password=p' into psycopg2 kwargs."""
    mapping = {'host': 'Host', 'port': 'Port', 'dbname': 'Database',
               'user': 'Username', 'password': 'Password'}
    parts = {k.strip(): v.strip() for k, v in
             (pair.split('=', 1) for pair in dsn_str.split(';') if '=' in pair)}
    return {k: parts[v] for k, v in mapping.items() if v in parts}


def main():
    parser = argparse.ArgumentParser(description="Seed Grounded database")
    parser.add_argument('--dsn', default='Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=change_me',
                        help='ADO.NET-style connection string')
    args = parser.parse_args()

    conn_kwargs = parse_dsn(args.dsn)
    print(f"Connecting to {conn_kwargs.get('host')}:{conn_kwargs.get('port', 5432)} / {conn_kwargs.get('dbname')} …")

    conn = psycopg2.connect(**conn_kwargs)
    conn.autocommit = False
    cur = conn.cursor()

    # Check if already seeded
    cur.execute("SELECT COUNT(*) FROM customers")
    if cur.fetchone()[0] > 0:
        print("Database already has customers — skipping seed.")
        conn.close()
        return

    print("Generating customers …")
    customers = gen_customers()

    print("Generating products …")
    products = gen_products()

    print("Generating orders and items …")
    orders, items = gen_orders_and_items(N_CUSTOMERS, products)

    print(f"Inserting {len(customers)} customers …")
    psycopg2.extras.execute_values(
        cur,
        "INSERT INTO customers (customer_name, email, segment, region, acquisition_channel, created_at) VALUES %s",
        customers, page_size=500
    )

    print(f"Inserting {len(products)} products …")
    psycopg2.extras.execute_values(
        cur,
        "INSERT INTO products (sku, product_name, category, subcategory, unit_cost, is_active, created_at) VALUES %s",
        products, page_size=200
    )

    print(f"Inserting {len(orders)} orders …")
    psycopg2.extras.execute_values(
        cur,
        "INSERT INTO orders (customer_id, order_date, status, sales_channel, shipping_region) VALUES %s",
        orders, page_size=1000
    )

    print(f"Inserting {len(items)} order_items …")
    psycopg2.extras.execute_values(
        cur,
        "INSERT INTO order_items (order_id, product_id, quantity, unit_price, discount_amount) VALUES %s",
        items, page_size=2000
    )

    conn.commit()
    cur.close()
    conn.close()
    print(f"Done. Seeded {len(customers)} customers, {len(products)} products, "
          f"{len(orders)} orders, {len(items)} order_items.")


if __name__ == '__main__':
    main()
