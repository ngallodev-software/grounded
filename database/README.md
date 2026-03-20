# Database

Postgres runs in Docker, managed from the repo. It stays private to localhost — the API talks to it directly; it is never exposed to the internet.

## Quick start

```bash
cp .env.example .env
# Edit .env and set a real POSTGRES_PASSWORD

bash scripts/db-up.sh       # start + wait for healthy
pip install psycopg2-binary
python scripts/seed.py --dsn "Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=<your-password>"
```

Then add the connection string to `Grounded.Api/appsettings.Local.json` (gitignored) or set the env var:

```json
{
  "ConnectionStrings": {
    "AnalyticsDatabase": "Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=<your-password>"
  }
}
```

## Helper scripts

| Script | Effect |
|---|---|
| `scripts/db-up.sh` | Start Postgres, wait for healthy, print connection string |
| `scripts/db-down.sh` | Stop Postgres (volume kept) |
| `scripts/db-reset.sh` | Destroy container + volume (destructive, prompts confirmation) |
| `scripts/db-logs.sh` | Tail Postgres logs |

## Init scripts

`database/init/` is mounted as `docker-entrypoint-initdb.d` and runs once on first container boot.

- `001-schema.sql` — creates the four analytics tables (`customers`, `products`, `orders`, `order_items`) and their indexes

The app's own tables (`llm_traces`, `eval_runs`, `conversation_states`) are created at startup by `SchemaInitializer` in the `grounded` schema.

## Seed data

`scripts/seed.py` generates realistic e-commerce data matching the spec in `docs/phases/phase-1-artifact.md`:

- 3,000 customers
- 180 products (15 premium, 12 inactive)
- ~36,000 orders (90% Completed, 6% Cancelled, 4% Refunded)
- ~97k–108k order_items

It is idempotent — re-running when customers already exist is a no-op.

## Backups

`database/backups/` is gitignored. To take a manual backup:

```bash
docker exec grounded-postgres pg_dump -U grounded grounded > database/backups/$(date +%Y%m%d).sql
```
