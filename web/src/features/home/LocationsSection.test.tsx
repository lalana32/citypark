import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CENTAR, createTestQueryClient, page, problem, type Route, stubApi } from '../../test/apiStub'
import { LocationsSection } from './LocationsSection'

const EVENING = { ...CENTAR, id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d02', name: 'Večernji', opensAt: '18:00:00' }

const availabilityOf = (id: string) => `GET /api/v1/lots/${id}/availability` as Route

function availability(availableSpaces: number) {
  return () =>
    Response.json({ lotId: CENTAR.id, start: '', end: '', totalSpaces: 30, availableSpaces, freeSpaces: [] })
}

function renderSection(routes: Record<Route, () => Response>) {
  const fetchMock = stubApi(routes)
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <LocationsSection />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return fetchMock
}

function requestedUrls(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls.map(([input]) => new URL(input instanceof Request ? input.url : String(input)))
}

beforeEach(() => {
  // 10:07 on the lot clock, so the default slot is 10:15–12:15.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-12-01T09:07:00Z'))
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('locations section', () => {
  it('shows a card per lot with address, price and total spaces', async () => {
    renderSection({
      'GET /api/v1/lots': () => Response.json(page([CENTAR])),
      [availabilityOf(CENTAR.id)]: availability(12),
    })

    const card = within(await screen.findByRole('listitem'))
    expect(card.getByRole('heading', { name: 'Centar' })).toBeDefined()
    expect(card.getByText('Ferhadija 1')).toBeDefined()
    expect(card.getByText('2.50 KM')).toBeDefined()
    expect(card.getByText('Total spaces').nextElementSibling?.textContent).toBe('30')
  })

  it('shows the photo of a known lot and the illustration for any other', async () => {
    renderSection({
      'GET /api/v1/lots': () => Response.json(page([CENTAR, EVENING])),
      [availabilityOf(CENTAR.id)]: availability(12),
    })

    const [centar, evening] = await screen.findAllByRole('listitem')
    expect(within(centar).getByRole('img', { name: 'Centar parking lot' }).getAttribute('src')).toContain('centar')
    expect(within(evening).queryByRole('img')).toBeNull()
  })

  it('shows the free spaces for the default slot', async () => {
    const fetchMock = renderSection({
      'GET /api/v1/lots': () => Response.json(page([CENTAR])),
      [availabilityOf(CENTAR.id)]: availability(12),
    })

    expect(await screen.findByText('12 free spaces')).toBeDefined()
    expect(screen.getByText('Free').nextElementSibling?.textContent).toBe('12')

    const url = requestedUrls(fetchMock).find((item) => item.pathname.endsWith('/availability'))
    expect(url?.searchParams.get('start')).toBe('2026-12-01T10:15:00+01:00')
    expect(url?.searchParams.get('end')).toBe('2026-12-01T12:15:00+01:00')
  })

  it('labels a lot that is closed for the slot and does not ask for its spaces', async () => {
    const fetchMock = renderSection({ 'GET /api/v1/lots': () => Response.json(page([EVENING])) })

    const card = within(await screen.findByRole('listitem'))
    expect(card.getByText('Closed at this time')).toBeDefined()
    expect(card.getByText('Free').nextElementSibling?.textContent).toBe('Closed')
    expect(requestedUrls(fetchMock).some((item) => item.pathname.endsWith('/availability'))).toBe(false)
  })

  it('links each card to its lot page', async () => {
    renderSection({
      'GET /api/v1/lots': () => Response.json(page([CENTAR])),
      [availabilityOf(CENTAR.id)]: availability(12),
    })

    const view = await screen.findByRole('link', { name: 'View parking: Centar' })
    expect(view.getAttribute('href')).toBe(`/lots/${CENTAR.id}`)
  })

  it('shows a loading state while the lots are on their way', () => {
    renderSection({ 'GET /api/v1/lots': () => Response.json(page([CENTAR])) })

    expect(screen.getByRole('status').textContent).toBe('Loading parking lots…')
  })

  it('shows an error when the lots cannot be loaded', async () => {
    renderSection({ 'GET /api/v1/lots': () => problem(500) })

    expect((await screen.findByRole('alert')).textContent).toBe('We could not load the parking lots. Please try again.')
  })

  it('says so when there are no lots yet', async () => {
    renderSection({ 'GET /api/v1/lots': () => Response.json(page([])) })

    expect(await screen.findByText('There are no parking lots yet.')).toBeDefined()
    expect(screen.queryByRole('list')).toBeNull()
  })
})
