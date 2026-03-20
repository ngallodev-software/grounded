-- Grounded analytics schema
-- Runs once on first container init (postgres:17 docker-entrypoint-initdb.d)

CREATE TABLE customers (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    segment TEXT NOT NULL CHECK (segment IN ('Consumer', 'SMB', 'Enterprise')),
    region TEXT NOT NULL CHECK (region IN ('West', 'Central', 'East', 'South')),
    acquisition_channel TEXT NOT NULL CHECK (
        acquisition_channel IN ('Organic', 'Paid Search', 'Email', 'Affiliate', 'Social')
    ),
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE products (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sku TEXT NOT NULL UNIQUE,
    product_name TEXT NOT NULL,
    category TEXT NOT NULL CHECK (
        category IN ('Electronics', 'Home', 'Office', 'Fitness', 'Accessories')
    ),
    subcategory TEXT NOT NULL,
    unit_cost NUMERIC(12, 2) NOT NULL CHECK (unit_cost >= 0),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE orders (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_id BIGINT NOT NULL REFERENCES customers(id),
    order_date TIMESTAMPTZ NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('Completed', 'Cancelled', 'Refunded')),
    sales_channel TEXT NOT NULL CHECK (sales_channel IN ('Web', 'Mobile', 'Marketplace')),
    shipping_region TEXT NOT NULL CHECK (shipping_region IN ('West', 'Central', 'East', 'South'))
);

CREATE TABLE order_items (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id BIGINT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id BIGINT NOT NULL REFERENCES products(id),
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    unit_price NUMERIC(12, 2) NOT NULL CHECK (unit_price >= 0),
    discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (
        discount_amount >= 0 AND discount_amount <= (quantity * unit_price)
    ),
    CONSTRAINT uq_order_items_order_product UNIQUE (order_id, product_id)
);

CREATE INDEX ix_orders_order_date ON orders(order_date);
CREATE INDEX ix_orders_customer_id ON orders(customer_id);
CREATE INDEX ix_order_items_order_id ON order_items(order_id);
CREATE INDEX ix_order_items_product_id ON order_items(product_id);
CREATE INDEX ix_products_category ON products(category);
CREATE INDEX ix_customers_region ON customers(region);
