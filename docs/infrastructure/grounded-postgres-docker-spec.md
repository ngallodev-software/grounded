# Grounded — Postgres in Docker (Repo-Managed) Spec

## Objective

Run Postgres for Grounded in a Docker container, managed from within the repo, while keeping it private to the local machine and fully compatible with Cloudflare Tunnel + Access.

This setup is intended for:
- local development
- private demo hosting from your machine
- simple, auditable infrastructure
- easy reset / bootstrap / backup behavior

It is **not** intended to expose Postgres directly to the internet.

---

## Architecture Fit

Cloudflare protects the **frontend / API entry point** only.

Postgres stays private behind the application boundary:

```text
Hiring Manager Browser
        ↓
Cloudflare Access
        ↓
Cloudflare Tunnel
        ↓
Frontend / API on your machine
        ↓
Postgres Docker container on your machine
```

This means:
- Cloudflare never fronts Postgres directly
- only the API talks to Postgres
- Postgres should not be internet-exposed
- the OpenAI API key stays on the server side only

---

## Recommended Repo Layout

```text
grounded/
├─ compose.yaml
├─ .env.example
├─ database/
│  ├─ init/
│  │  └─ 001-init.sql
│  ├─ README.md
│  └─ backups/
├─ scripts/
│  ├─ db-up.ps1
│  ├─ db-down.ps1
│  ├─ db-reset.ps1
│  └─ db-logs.ps1
└─ ...
```

You can also use shell scripts or Make targets instead of PowerShell if preferred.

---

## Recommended Docker Compose Spec

Use a repo-managed `compose.yaml` with:
- a named volume
- localhost-only port binding
- healthcheck
- optional init scripts

Example:

```yaml
services:
  postgres:
    image: postgres:17
    container_name: grounded-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: grounded
      POSTGRES_USER: grounded
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - grounded_pg_data:/var/lib/postgresql/data
      - ./database/init:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U grounded -d grounded"]
      interval: 10s
      timeout: 5s
      retries: 10
    ports:
      - "127.0.0.1:5432:5432"

volumes:
  grounded_pg_data:
```

### Why bind to `127.0.0.1`
This keeps Postgres reachable from your machine and local tools, but not broadly exposed on the LAN or internet.

Prefer this:

```yaml
ports:
  - "127.0.0.1:5432:5432"
```

Avoid this:

```yaml
ports:
  - "5432:5432"
```

unless you explicitly want broader network exposure.

---

## Environment Variables

Recommended `.env.example`:

```env
POSTGRES_PASSWORD=change_me
POSTGRES_DB=grounded
POSTGRES_USER=grounded
POSTGRES_PORT=5432
```

If your app also uses env-driven DB config, include:

```env
ConnectionStrings__GroundedDb=Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=change_me
```

Use a real `.env` locally and never commit secrets.

---

## Connection Strategy

### If API runs outside Docker
Use:

```text
Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=...
```

### If API runs inside Docker later
Use the Docker service hostname instead:

```text
Host=postgres;Port=5432;Database=grounded;Username=grounded;Password=...
```

For now, the first option is the cleanest fit.

---

## Data Persistence

Use a named Docker volume for database persistence:

- survives container restarts
- easy to manage locally
- keeps DB state independent from the container lifecycle

Recommended volume name:

```text
grounded_pg_data
```

---

## Init Scripts

Use `database/init/` for first-boot initialization only.

Good candidates:
- schema bootstrap for local demo setup
- reference tables
- seed data if needed for the demo

Avoid putting evolving app migrations here if the app already has a migration system. Use init scripts only for first container initialization or demo bootstrap.

---

## Healthcheck

A DB healthcheck is important because:
- the API may start before Postgres is ready
- local demo startup becomes less flaky
- app/service orchestration becomes more reliable

Recommended command:

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U grounded -d grounded"]
  interval: 10s
  timeout: 5s
  retries: 10
```

---

## Local Operations

Recommended commands:

### Start DB

```bash
docker compose up -d postgres
```

### Stop DB

```bash
docker compose down
```

### View logs

```bash
docker compose logs -f postgres
```

### Reset DB (destructive)

```bash
docker compose down -v
```

You may want helper scripts for these so the repo feels polished.

---

## Suggested Helper Scripts

### `scripts/db-up.ps1`
- starts postgres
- prints connection info
- optionally waits for healthcheck

### `scripts/db-down.ps1`
- stops postgres

### `scripts/db-reset.ps1`
- tears down container + volume
- warns before deletion

### `scripts/db-logs.ps1`
- tails postgres logs

This improves local dev UX and interview/demo polish.

---

## Security Guidance

### Do
- keep Postgres private
- bind to localhost only
- use strong local password
- keep secrets in `.env`
- keep OpenAI key only in backend env/config

### Do not
- expose Postgres publicly
- pass OpenAI credentials to frontend
- store secrets in committed config
- rely on Cloudflare to protect the DB itself

Cloudflare protects the HTTP app edge, not the local database directly.

---

## Compatibility With Cloudflare Tunnel + Access

This setup is fully compatible with the planned deployment model.

Cloudflare handles:
- public hostname
- auth gate
- access control to the app

Your machine handles:
- frontend
- backend
- Postgres container

Postgres stays entirely local/private.

That is the correct separation of concerns for this demo.

---

## Recommended Next Additions

To make this feel complete in-repo, add:

1. `compose.yaml`
2. `.env.example`
3. `database/README.md`
4. `database/init/`
5. `scripts/db-up.ps1`
6. `scripts/db-down.ps1`
7. `scripts/db-reset.ps1`
8. `scripts/db-logs.ps1`

---

## Optional Future Upgrades

Later, if needed:
- add pgAdmin or Adminer for local-only inspection
- add a backup script
- move API into Docker too
- add migration container or startup migration step

Do not do those unless they materially help the demo.

---

## Recommended Final Position

For Grounded, the best setup is:

- Postgres in Docker
- managed from the repo
- bound to localhost only
- frontend / API on your machine
- Cloudflare Tunnel + Access in front of the web app only

This gives you:
- a strong local developer experience
- a safe private demo setup
- a good interview story about infrastructure boundaries
