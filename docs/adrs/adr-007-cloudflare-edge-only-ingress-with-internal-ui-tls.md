# ADR-007: Cloudflare edge-only ingress with internal UI TLS

## Status
Accepted

## Date
2026-03-23

## Context
The application is deployed behind Cloudflare Tunnel and is meant to be reached through a public HTTPS hostname. The repo now includes:

- a `cloudflared` tunnel service in `compose.yaml`
- an nginx-based UI container that listens on both `80` and `443`
- mounted Cloudflare Origin CA certificate files for the UI container
- same-origin browser requests from the UI to `/analytics/*`
- a verification script and `make check-https` target for local and public ingress checks

There are several ways to wire this ingress path:

1. Terminate TLS at Cloudflare and proxy from the tunnel to the UI over plain HTTP.
2. Terminate TLS at Cloudflare and proxy from the tunnel to the UI over HTTPS using an origin certificate.
3. Expose the API directly and let the browser call it cross-origin.
4. Put the UI behind Cloudflare but keep the API on a separate public host.

The system needs one consistent deployment model that avoids mixed content, keeps the browser on same-origin paths, and does not expose the API directly to the public internet.

## Decision
Cloudflare is the public TLS terminator, and the tunnel forwards traffic to the nginx UI container over HTTPS on port `443`.

The UI container is responsible for serving the frontend and for proxying `/analytics/*` to the API container over Docker networking. The API is not exposed as a public origin; it remains reachable only on `127.0.0.1:5252` from the host and over the internal Docker network from nginx.

The UI container must mount a valid origin certificate and key at:

- `/etc/nginx/certs/tls.crt`
- `/etc/nginx/certs/tls.key`

The frontend build must keep `VITE_API_BASE_URL` empty so browser requests resolve to same-origin `/analytics/*` URLs. In production, the browser should never need a separate API host.

The ingress path is verified by `scripts/check-cloudflare-https.sh` and the `make check-https` / `make check-https-public` targets.

## Consequences

### Positive
- Cloudflare remains the only public edge for the application.
- The browser uses same-origin API requests, which avoids CORS and mixed-content issues.
- The UI container can validate its own HTTPS origin behavior independently of the tunnel.
- The API stays private to the host and Docker network, reducing direct exposure.
- The deployment is easy to verify locally and remotely with a single scripted check.

### Negative
- The UI container now needs certificate material mounted into the container filesystem.
- Local deployment requires the origin certificate files to be present on the host.
- The nginx configuration has to be kept in sync with the tunnel target and the API upstream path.
- TLS is terminated twice in the path: at Cloudflare and again at nginx.

## Alternatives considered

### 1. Cloudflare terminates TLS and tunnels to the UI over HTTP
Rejected because it weakens the origin path and makes the deployment less representative of the intended edge-only HTTPS model. The current design keeps the tunnel-to-origin hop encrypted.

### 2. Expose the API as a separate public origin
Rejected because it increases the attack surface, introduces cross-origin browser traffic, and complicates deployment and auth.

### 3. Use the browser to call the API directly on a public hostname
Rejected because it makes the UI more fragile, requires CORS handling, and duplicates ingress concerns across two public endpoints.

### 4. Serve the UI only on HTTP internally
Rejected because the repo now expects an HTTPS origin for nginx and validates that path explicitly. Keeping the origin on HTTPS reduces mismatch between local verification and production behavior.
