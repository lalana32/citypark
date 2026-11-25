import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CENTAR, createTestQueryClient, stubApi } from '../../test/apiStub'
import { useQuote } from './api'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={createTestQueryClient()}>{children}</QueryClientProvider>
}

const interval = { start: '2026-12-01T10:00:00+01:00', end: '2026-12-01T11:15:00+01:00' }
const quote = { lotId: CENTAR.id, ...interval, billedHours: 2, price: 5, currency: 'BAM' }

describe('useQuote', () => {
  it('returns the price for the interval', async () => {
    const fetchMock = stubApi({ [`GET /api/v1/lots/${CENTAR.id}/quote`]: () => Response.json(quote) })

    const { result } = renderHook(() => useQuote(CENTAR.id, interval), { wrapper })

    await waitFor(() => expect(result.current.data).toEqual(quote))
    const request = fetchMock.mock.calls[0][0] as Request
    expect(new URL(request.url).searchParams.get('start')).toBe(interval.start)
  })

  it('does not ask for a price before there is an interval', () => {
    const fetchMock = stubApi({})

    const { result } = renderHook(() => useQuote(CENTAR.id, null), { wrapper })

    expect(result.current.fetchStatus).toBe('idle')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('fails on a server error', async () => {
    stubApi({ [`GET /api/v1/lots/${CENTAR.id}/quote`]: () => new Response(null, { status: 500 }) })

    const { result } = renderHook(() => useQuote(CENTAR.id, interval), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
