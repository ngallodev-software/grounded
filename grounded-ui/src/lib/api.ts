import type { QueryRequest, QueryResponse } from '@/types/api'

function getApiBase(): string {
  const configuredBase = import.meta.env.VITE_API_BASE_URL?.trim()
  if (configuredBase) {
    return configuredBase.replace(/\/$/, '')
  }

  const appBase = import.meta.env.BASE_URL.replace(/\/$/, '')
  return `${appBase}/analytics`
}

// Default API requests to the app's configured base path so deployments under
// /grounded/ hit /grounded/analytics/* instead of the domain root /analytics/*.
const API_BASE = getApiBase()

export async function postQuery(req: QueryRequest): Promise<QueryResponse> {
  const res = await fetch(`${API_BASE}/query`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })

  if (!res.ok && res.status !== 422) {
    throw new Error(`Request failed: ${res.status} ${res.statusText}`)
  }

  return res.json() as Promise<QueryResponse>
}
