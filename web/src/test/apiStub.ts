import { QueryClient } from '@tanstack/react-query'
import { vi } from 'vitest'

export type Route = `${string} ${string}`

export const ANA = { id: '7c9e6679-7425-40de-944b-e07fc1f90ae7', email: 'ana@example.com', roles: ['Customer'] }

export const csrfRoute = { 'GET /api/v1/auth/csrf': () => Response.json({ token: 'test-token' }) }

/** Answers each "METHOD /path" with its handler and everything else with 404. */
export function stubApi(routes: Record<Route, () => Response>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input instanceof Request ? input : new Request(input)
    const route = `${request.method} ${new URL(request.url).pathname}` as Route
    const respond = routes[route] ?? (() => new Response(null, { status: 404 }))
    return respond()
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export function problem(status: number, body: object = {}, headers: Record<string, string> = {}) {
  return Response.json(body, { status, headers: { 'Content-Type': 'application/problem+json', ...headers } })
}

export function createTestQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
}
