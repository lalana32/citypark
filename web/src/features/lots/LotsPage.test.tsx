import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CENTAR, createTestQueryClient, page, problem, type Route, stubApi } from '../../test/apiStub'
import { LotsPage } from './LotsPage'

const centarAvailability = {
  [`GET /api/v1/lots/${CENTAR.id}/availability` as Route]: () =>
    Response.json({ lotId: CENTAR.id, start: '', end: '', totalSpaces: 30, availableSpaces: 12, freeSpaces: [] }),
}

beforeEach(() => {
  // 10:07 on the lot clock, so Centar is open for the default slot.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-12-01T09:07:00Z'))
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <LotsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('LotsPage', () => {
  it('shows a card per lot with its price, spaces, free spaces and link', async () => {
    stubApi({ 'GET /api/v1/lots': () => Response.json(page([CENTAR])), ...centarAvailability })
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Our locations' })).toBeDefined()
    const card = within(await screen.findByRole('listitem'))
    expect(card.getByRole('heading', { name: 'Centar' })).toBeDefined()
    expect(card.getByText('2.50 KM')).toBeDefined()
    expect(card.getByText('Total spaces').nextElementSibling?.textContent).toBe('30')
    expect(await card.findByText('12 free spaces')).toBeDefined()
    expect(card.getByRole('link', { name: 'View parking: Centar' }).getAttribute('href')).toBe(`/lots/${CENTAR.id}`)
  })

  it('shows a loading state until the lots arrive', async () => {
    stubApi({ 'GET /api/v1/lots': () => Response.json(page([CENTAR])), ...centarAvailability })
    renderPage()

    expect(screen.getByRole('status').textContent).toBe('Loading parking lots…')
    expect(await screen.findByRole('heading', { name: 'Centar' })).toBeDefined()
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows an error when the lots cannot be loaded', async () => {
    stubApi({ 'GET /api/v1/lots': () => problem(500) })
    renderPage()

    expect((await screen.findByRole('alert')).textContent).toBe('We could not load the parking lots. Please try again.')
  })

  it('says so when there are no lots', async () => {
    stubApi({ 'GET /api/v1/lots': () => Response.json(page([])) })
    renderPage()

    expect(await screen.findByText('There are no parking lots yet.')).toBeDefined()
  })

  it('asks for the next page', async () => {
    const fetchMock = stubApi({ 'GET /api/v1/lots': () => Response.json(page([CENTAR], 2)), ...centarAvailability })
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Next' }))

    await vi.waitFor(() => {
      const pages = fetchMock.mock.calls
        .map(([input]) => new URL(input instanceof Request ? input.url : String(input)))
        .filter((url) => url.pathname === '/api/v1/lots')
        .map((url) => url.searchParams.get('page'))
      expect(pages).toEqual(['1', '2'])
    })
  })
})
