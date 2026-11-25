import { QueryClient } from '@tanstack/react-query'
import { vi } from 'vitest'

export type Route = `${string} ${string}`

export const ANA = { id: '7c9e6679-7425-40de-944b-e07fc1f90ae7', email: 'ana@example.com', roles: ['Customer'] }

export const ADMIN = { id: '3f2504e0-4f89-41d3-9a0c-0305e82c3301', email: 'admin@example.com', roles: ['Admin'] }

export const CENTAR = {
  id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01',
  name: 'Centar',
  address: 'Ferhadija 1',
  hourlyPrice: 2.5,
  currency: 'BAM',
  opensAt: '07:00:00',
  closesAt: '22:00:00',
  totalSpaces: 30,
}

type TestSpace = { id: string; label: string }

/** An availability answer of CENTAR: the free spaces, plus occupied ones that only show in `spaces`. */
export function availabilityOf(free: TestSpace[], occupied: TestSpace[] = []) {
  const spaces = [
    ...free.map((space) => ({ ...space, isFree: true })),
    ...occupied.map((space) => ({ ...space, isFree: false })),
  ].sort((a, b) => a.label.localeCompare(b.label))

  return Response.json({
    lotId: CENTAR.id,
    start: '2026-12-01T07:00:00+01:00',
    end: '2026-12-01T08:00:00+01:00',
    totalSpaces: spaces.length,
    availableSpaces: free.length,
    freeSpaces: free,
    spaces,
  })
}

export function page<T>(items: T[], totalPages = 1) {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages }
}

export const csrfRoute = { 'GET /api/v1/auth/csrf': () => Response.json({ token: 'test-token' }) }

/** Answers each "METHOD /path" with its handler and everything else with 404. */
export type Handler = () => Response | Promise<Response>

export function stubApi(routes: Record<Route, Handler>) {
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
