import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { ANA, createTestQueryClient, csrfRoute, page, problem, type Route as ApiRoute, stubApi } from '../../test/apiStub'
import type { Reservation } from './api'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const ID = '019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01'
const LIST = 'GET /api/v1/reservations'
const CANCEL = `POST /api/v1/reservations/${ID}/cancel`

function reservation(overrides: Partial<Reservation> = {}): Reservation {
  return {
    id: ID,
    lotName: 'Centar',
    spaceLabel: 'A-02',
    start: '2099-12-01T09:00:00+00:00',
    end: '2099-12-01T10:30:00+00:00',
    plate: 'A12K345',
    price: 5,
    currency: 'BAM',
    state: 'Upcoming',
    ...overrides,
  }
}

function routes(list: () => Response, cancel: () => Response = () => new Response(null, { status: 204 })) {
  const result: Record<ApiRoute, () => Response> = {
    ...csrfRoute,
    'GET /api/v1/auth/me': () => Response.json(ANA),
    [LIST]: list,
  }
  result[CANCEL] = cancel
  return result
}

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={['/reservations']}>
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

describe('my reservations', () => {
  it('shows each reservation in lot time with its state and offers the cancel only when allowed', async () => {
    const soon = new Date(Date.now() + 10 * 60_000).toISOString()
    stubApi(
      routes(() =>
        Response.json(
          page([
            reservation(),
            reservation({ id: 'b', spaceLabel: 'B-01', state: 'Finished' }),
            reservation({ id: 'c', spaceLabel: 'C-03', start: soon }),
          ]),
        ),
      ),
    )
    renderPage()

    const upcoming = await rowOf('A-02')
    expect(within(upcoming).getByText('1 Dec 2099, 10:00')).toBeDefined()
    expect(within(upcoming).getByText('Upcoming')).toBeDefined()
    expect(within(upcoming).getByRole('button', { name: 'Cancel' })).toBeDefined()
    expect(within(await rowOf('B-01')).queryByRole('button', { name: 'Cancel' })).toBeNull()
    expect(within(await rowOf('C-03')).queryByRole('button', { name: 'Cancel' })).toBeNull()
  })

  it('cancels after the confirmation and updates the badge without a reload', async () => {
    let cancelled = false
    const fetchMock = stubApi(
      routes(
        () => Response.json(page([reservation({ state: cancelled ? 'Cancelled' : 'Upcoming' })])),
        () => {
          cancelled = true
          return new Response(null, { status: 204 })
        },
      ),
    )
    renderPage()

    fireEvent.click(within(await rowOf('A-02')).getByRole('button', { name: 'Cancel' }))
    expect(calls(fetchMock, CANCEL)).toHaveLength(0)
    fireEvent.click(screen.getByRole('button', { name: 'Yes, cancel' }))

    const row = await rowOf('A-02')
    await waitFor(() => expect(within(row).getByText('Cancelled')).toBeDefined())
    expect(within(row).queryByRole('button', { name: 'Cancel' })).toBeNull()
    expect(calls(fetchMock, CANCEL)).toHaveLength(1)
    expect(calls(fetchMock, LIST)).toHaveLength(2)
  })

  it('keeps the reservation when the user changes their mind', async () => {
    const fetchMock = stubApi(routes(() => Response.json(page([reservation()]))))
    renderPage()

    fireEvent.click(within(await rowOf('A-02')).getByRole('button', { name: 'Cancel' }))
    fireEvent.click(screen.getByRole('button', { name: 'Keep it' }))

    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDefined()
    expect(calls(fetchMock, CANCEL)).toHaveLength(0)
  })

  it('shows the server message when the reservation started meanwhile', async () => {
    const message = 'A customer can cancel a reservation only before it starts (R9).'
    let started = false
    stubApi(
      routes(
        () => Response.json(page([reservation({ state: started ? 'InProgress' : 'Upcoming' })])),
        () => {
          started = true
          return problem(422, { detail: message, errors: { general: [message] } })
        },
      ),
    )
    renderPage()

    fireEvent.click(within(await rowOf('A-02')).getByRole('button', { name: 'Cancel' }))
    fireEvent.click(screen.getByRole('button', { name: 'Yes, cancel' }))

    expect((await screen.findByRole('alert')).textContent).toBe(message)
    await waitFor(() => expect(screen.getByText('In progress')).toBeDefined())
    expect(screen.queryByRole('button', { name: 'Cancel' })).toBeNull()
  })

  it('sends the user to sign in when the session has ended', async () => {
    stubApi(routes(() => Response.json(page([reservation()])), () => problem(401)))
    renderPage()

    fireEvent.click(within(await rowOf('A-02')).getByRole('button', { name: 'Cancel' }))
    fireEvent.click(screen.getByRole('button', { name: 'Yes, cancel' }))

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeDefined()
  })

  it('asks for the next page', async () => {
    const fetchMock = stubApi(routes(() => Response.json(page([reservation()], 2))))
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Next' }))

    await waitFor(() => expect(calls(fetchMock, LIST)).toHaveLength(2))
    expect(new URL(calls(fetchMock, LIST)[1].url).searchParams.get('page')).toBe('2')
    expect(screen.getByText('Page 2 of 2')).toBeDefined()
  })

  it('says so when there are no reservations', async () => {
    stubApi(routes(() => Response.json(page([]))))
    renderPage()

    expect(await screen.findByText(/You have no reservations yet/)).toBeDefined()
    expect(screen.getByRole('link', { name: 'Find a parking lot' }).getAttribute('href')).toBe('/lots')
  })
})
