# Cloudflare HTTPS Checklist

Use this when deploying the app with Cloudflare as the TLS terminator.

## Edge and tunnel

- [ ] Create or reuse a Cloudflare Tunnel for the app.
- [ ] Export `CLOUDFLARE_TUNNEL_TOKEN` into `.env` or the deployment environment.
- [ ] Confirm the tunnel routes the public hostname to the `grounded-ui` service on `https://ui:443`.
- [ ] Prefer a Cloudflare Origin CA certificate: `SSL/TLS` → `Origin Server` → `Create certificate`.
- [ ] Store the cert and key on the server at `/etc/cloudflare/origin/ngallodev-software.uk.pem` and `/etc/cloudflare/origin/ngallodev-software.uk.key`.
- [ ] Verify the tunnel shows active registered connections in `cloudflared` logs.

## UI and API

- [ ] Keep `grounded-ui` serving the app on HTTP 80 and TLS 443 internally.
- [ ] Keep `grounded-api` published only on `127.0.0.1:5252` for host access, with container-to-container traffic over Docker networking.
- [ ] Leave `VITE_API_BASE_URL` empty in the Docker build so the browser uses same-origin `/grounded/analytics/*` requests.
- [ ] Confirm nginx forwards `/analytics/*` to the API container over HTTP.
- [ ] Confirm nginx does not emit redirects that rewrite the host or port.

## Public URL

- [ ] Confirm the public URL is served over `https://`.
- [ ] Confirm the UI loads at the expected path, for example `https://ngallodev-software.uk/grounded/`.
- [ ] Confirm API calls succeed from the browser without mixed-content warnings.
- [ ] Confirm Cloudflare Access or other auth is in place if the hostname is meant to be private.

## Verification

- [ ] Run `make check-https` for local verification.
- [ ] Optionally set `PUBLIC_URL=https://...` and rerun `make check-https` to verify the public HTTPS URL.
- [ ] Use `PUBLIC_URL=https://... make check-https-public` to verify only the live public URL when the tunnel is already running.
- [ ] Confirm the local TLS check inside the `ui` container passes with `make check-https`.
- [ ] Check `docker compose logs --tail=50 cloudflared` for a live tunnel connection.
- [ ] Open the public URL and verify the UI renders.
- [ ] Submit an analytics query and verify the `/analytics/query` request returns `200`.
- [ ] Confirm the browser sees HTTPS only; no direct HTTP redirect or mixed content.
