import type { QueryRequest, QueryResponse } from '@/types/api'

// In Docker, VITE_API_BASE_URL is '' so requests are relative (proxied by nginx).
// In local dev, falls back to localhost:5252 (proxied by Vite dev server).
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5252'

export async function postQuery(req: QueryRequest): Promise<QueryResponse> {
  const res = await fetch(`${API_BASE}/analytics/query`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })

  if (!res.ok && res.status !== 422) {
    throw new Error(`Request failed: ${res.status} ${res.statusText}`)
  }

  return res.json() as Promise<QueryResponse>
}
