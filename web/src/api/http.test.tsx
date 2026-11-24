import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider, useMutation, useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { api } from './client'
import { CSRF_HEADER } from './http'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = input instanceof Request ? input.url : input.toString()
    return url.endsWith('/api/v1/auth/csrf')
      ? Response.json({ token: 'test-token' })
      : Response.json({ id: '1', email: 'ana@example.com', roles: ['Customer'] })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderWithQueryClient(ui: ReactNode) {
  render(<QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>)
}

function LogoutButton() {
  const logout = useMutation({ mutationFn: () => api.POST('/api/v1/auth/logout') })
  return <button onClick={() => logout.mutate()}>Log out</button>
}

function CurrentUser() {
  const me = useQuery({ queryKey: ['me'], queryFn: () => api.GET('/api/v1/auth/me') })
  return <p>{me.data?.data?.email}</p>
}

it('sends the CSRF token on an unsafe request', async () => {
  const fetchMock = stubFetch()
  renderWithQueryClient(<LogoutButton />)

  fireEvent.click(screen.getByRole('button', { name: 'Log out' }))

  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
  const sent = fetchMock.mock.calls[1][0] as Request
  expect(sent.method).toBe('POST')
  expect(new URL(sent.url).pathname).toBe('/api/v1/auth/logout')
  expect(sent.headers.get(CSRF_HEADER)).toBe('test-token')
  expect(localStorage.length).toBe(0)
  expect(sessionStorage.length).toBe(0)
})

it('sends a safe request without fetching a token', async () => {
  const fetchMock = stubFetch()
  renderWithQueryClient(<CurrentUser />)

  expect(await screen.findByText('ana@example.com')).toBeDefined()
  expect(fetchMock).toHaveBeenCalledTimes(1)
  expect((fetchMock.mock.calls[0][0] as Request).headers.has(CSRF_HEADER)).toBe(false)
})
