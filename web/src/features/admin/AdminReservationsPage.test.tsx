import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { ADMIN, createTestQueryClient, csrfRoute, page, problem, type Route as ApiRoute, stubApi } from '../../test/apiStub'
import { ALREADY_CANCELLED } from '../reservations/api'
import type { AdminReservation } from './api'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const ID = '019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01'
const LIST = 'GET /api/v1/admin/reservations'
const CANCEL = `POST /api/v1/admin/reservations/${ID}/cancel`

function reservation(overrides: Partial<AdminReservation> = {}): AdminReservation {
  return {
    id: ID,
    userId: '019ad0a3-2f1e-7c4d-8b5a-6e7f8091a2b3',
    lotId: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01',
    lotName: 'Centar',
    spaceId: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02',
    spaceLabel: 'A-02',
    start: '2026-12-01T09:00:00+00:00',
    end: '2026-12-01T10:30:00+00:00',
    plate: 'A12K345',
    price: 5,
    currency: 'BAM',
    status: 'Active',
    state: 'InProgress',
    ...overrides,
  }
}

function routes(list: () => Response, cancel: () => Response = () => Response.json(reservation({ status: 'Cancelled', state: 'Cancelled' }))) {
  const result: Record<ApiRoute, () => Response> = {
    ...csrfRoute,
    'GET /api/v1/auth/me': () => Response.json(ADMIN),
    [LIST]: list,
  }
  result[CANCEL] = cancel
  return result
}

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={['/admin/reservations']}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function rowOf(spaceLabel: string) {
  return (await screen.findByText(spaceLabel)).closest('tr')!
}

function calls(fetchMock: ReturnType<typeof stubApi>, route: string) {
  return fetchMock.mock.calls
    .map(([input]) => (input instanceof Request ? input : new Request(input)))
    .filter((request) => `${request.method} ${new URL(request.url).pathname}` === route)
}

function cancelRow(row: HTMLElement) {
  fireEvent.click(within(row).getByRole('button', { name: 'Cancel' }))
  fireEvent.click(within(row).getByRole('button', { name: 'Yes, cancel' }))
}

describe('admin reservations', () => {
  it('sets the tab title', async () => {
    stubApi(routes(() => Response.json(page([]))))
    renderPage()

    expect(await screen.findByText('There are no reservations yet.')).toBeDefined()
    expect(document.title).toBe('All reservations · CityPark')
  })

  it('offers the cancel for upcoming and in-progress reservations only', async () => {
    stubApi(
      routes(() =>
        Response.json(
          page([
            reservation({ id: 'a', spaceLabel: 'A-01', state: 'Upcoming' }),
            reservation({ id: 'b', spaceLabel: 'B-01', state: 'InProgress' }),
            reservation({ id: 'c', spaceLabel: 'C-01', state: 'Finished' }),
            reservation({ id: 'd', spaceLabel: 'D-01', status: 'Cancelled', state: 'Cancelled' }),
          ]),
        ),
      ),
    )
    renderPage()

    expect(within(await rowOf('A-01')).getByRole('button', { name: 'Cancel' })).toBeDefined()
    expect(within(await rowOf('B-01')).getByRole('button', { name: 'Cancel' })).toBeDefined()
    expect(within(await rowOf('C-01')).queryByRole('button', { name: 'Cancel' })).toBeNull()
    expect(within(await rowOf('D-01')).queryByRole('button', { name: 'Cancel' })).toBeNull()
  })

  it("cancels another user's reservation that has already started", async () => {
    let cancelled = false
    const fetchMock = stubApi(
      routes(
        () => Response.json(page([reservation(cancelled ? { status: 'Cancelled', state: 'Cancelled' } : {})])),
        () => {
          cancelled = true
          return Response.json(reservation({ status: 'Cancelled', state: 'Cancelled' }))
        },
      ),
    )
    renderPage()

    cancelRow(await rowOf('A-02'))

    const row = await rowOf('A-02')
    await waitFor(() => expect(within(row).getByText('Cancelled')).toBeDefined())
    expect(within(row).queryByRole('button', { name: 'Cancel' })).toBeNull()
    expect(calls(fetchMock, CANCEL)).toHaveLength(1)
    expect(calls(fetchMock, LIST)).toHaveLength(2)
  })

  it('says so when someone else cancelled it first', async () => {
    stubApi(routes(() => Response.json(page([reservation()])), () => problem(409)))
    renderPage()

    cancelRow(await rowOf('A-02'))

    expect((await screen.findByRole('alert')).textContent).toBe(ALREADY_CANCELLED)
  })
})
