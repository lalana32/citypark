import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CENTAR, createTestQueryClient, problem, type Route as ApiRoute, stubApi } from '../../test/apiStub'
import { LOT_NOT_FOUND } from './api'
import { LotAvailabilityPage } from './LotAvailabilityPage'
import { NO_FREE_SPACES } from './SpaceGrid'
import { QUOTE_FAILED } from './SummaryPanel'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

/** The lot route, plus the availability and quote routes when they have an answer. */
function routes(respond?: () => Response, quote?: () => Response): Record<ApiRoute, () => Response> {
  const result: Record<ApiRoute, () => Response> = {}
  result[`GET /api/v1/lots/${CENTAR.id}`] = () => Response.json(CENTAR)
  if (respond) {
    result[`GET /api/v1/lots/${CENTAR.id}/availability`] = respond
  }
  if (quote) {
    result[`GET /api/v1/lots/${CENTAR.id}/quote`] = quote
  }
  return result
}

function quoteOf(price: number, billedHours: number) {
  return () =>
    Response.json({
      lotId: CENTAR.id,
      start: '2026-12-01T07:00:00+01:00',
      end: '2026-12-01T08:00:00+01:00',
      billedHours,
      price,
      currency: 'BAM',
    })
}

function summaryRow(term: string): string | null {
  const summary = screen.getByRole('complementary', { name: 'Your parking' })
  const dt = Array.from(summary.querySelectorAll('dt')).find((node) => node.textContent === term)
  return dt?.nextElementSibling?.textContent ?? null
}

function availability(freeSpaces: { id: string; label: string }[]) {
  return Response.json({
    lotId: CENTAR.id,
    start: '2026-12-01T07:00:00+01:00',
    end: '2026-12-01T08:00:00+01:00',
    totalSpaces: CENTAR.totalSpaces,
    availableSpaces: freeSpaces.length,
    freeSpaces,
  })
}

const TWO_SPACES = [
  { id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02', label: 'A-02' },
  { id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e03', label: 'A-03' },
]

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[`/lots/${CENTAR.id}`]}>
        <Routes>
          <Route path="/lots/:id" element={<LotAvailabilityPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function check() {
  fireEvent.click(await screen.findByRole('button', { name: 'Check availability' }))
}

describe('LotAvailabilityPage', () => {
  it('shows the free spaces for the chosen interval in lot time', async () => {
    const fetchMock = stubApi(
      routes(() => availability([{ id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02', label: 'A-02' }])),
    )
    renderPage()

    await check()

    expect(await screen.findByText('1 of 30 spaces free')).toBeDefined()
    expect(screen.getByRole('button', { name: 'Space A-02' }).getAttribute('aria-pressed')).toBe('false')
    const request = fetchMock.mock.calls.at(-1)![0] as Request
    const query = new URL(request.url).searchParams
    expect(query.get('start')).toMatch(/T07:00:00\+0[12]:00$/)
    expect(query.get('end')).toMatch(/T08:00:00\+0[12]:00$/)
  })

  it('says so clearly when no space is free', async () => {
    stubApi(routes(() => availability([])))
    renderPage()

    await check()

    expect(await screen.findByText(NO_FREE_SPACES)).toBeDefined()
    expect(screen.getByText('Try an earlier or later time, or another date.')).toBeDefined()
    expect(screen.queryByRole('button', { name: /^Space / })).toBeNull()
  })

  it('highlights the chosen space and only that one', async () => {
    stubApi(routes(() => availability(TWO_SPACES)))
    renderPage()

    await check()
    fireEvent.click(await screen.findByRole('button', { name: 'Space A-02' }))
    fireEvent.click(screen.getByRole('button', { name: 'Space A-03' }))

    expect(screen.getByRole('button', { name: 'Space A-02' }).getAttribute('aria-pressed')).toBe('false')
    expect(screen.getByRole('button', { name: 'Space A-03' }).getAttribute('aria-pressed')).toBe('true')
  })

  it('drops the spaces and the choice when the time changes', async () => {
    stubApi(routes(() => availability(TWO_SPACES)))
    renderPage()

    await check()
    fireEvent.click(await screen.findByRole('button', { name: 'Space A-02' }))
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '09:00' } })

    expect(screen.queryByRole('button', { name: 'Space A-02' })).toBeNull()
    await check()
    expect((await screen.findByRole('button', { name: 'Space A-02' })).getAttribute('aria-pressed')).toBe('false')
  })

  it('shows a 422 field error next to its field', async () => {
    const message = 'End (07:15:00) must fall on the 15-minute grid (:00, :15, :30, :45) (R1).'
    stubApi(routes(() => problem(422, { detail: message, errors: { end: [message] } })))
    renderPage()

    await check()

    expect(await screen.findByText(message)).toBeDefined()
    expect(screen.getByLabelText('To').getAttribute('aria-invalid')).toBe('true')
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows a 422 error without a field above the form', async () => {
    const message = 'A reservation must last at least 30 minutes, but lasts 15.'
    stubApi(routes(() => problem(422, { detail: message, errors: { general: [message] } })))
    renderPage()

    await check()

    expect((await screen.findByRole('alert')).textContent).toBe(message)
    expect(screen.getByRole('button', { name: 'Check availability' })).toBeDefined()
  })

  it('clears the error once the interval changes', async () => {
    const message = 'A reservation must last at least 30 minutes, but lasts 15.'
    stubApi(routes(() => problem(422, { detail: message, errors: { general: [message] } })))
    renderPage()

    await check()
    await screen.findByRole('alert')
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '09:00' } })

    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('fills the panel with the chosen space, the times and the price from the quote', async () => {
    const fetchMock = stubApi(routes(() => availability(TWO_SPACES), quoteOf(2.5, 1)))
    renderPage()

    expect(await screen.findByText('Pick a time and check availability, then choose a free space.')).toBeDefined()
    await check()
    fireEvent.click(await screen.findByRole('button', { name: 'Space A-03' }))

    expect(await screen.findByText('2.50 BAM')).toBeDefined()
    expect(summaryRow('Space')).toBe('A-03')
    expect(summaryRow('Arrival')).toBe('07:00')
    expect(summaryRow('Departure')).toBe('08:00')
    expect(summaryRow('Duration')).toBe('1 h')
    expect(summaryRow('Price')).toBe('2.50 BAM (1 h billed)')
    const [quoteCall] = fetchMock.mock.calls
      .map(([input]) => input as Request)
      .filter((request) => new URL(request.url).pathname.endsWith('/quote'))
    expect(new URL(quoteCall.url).searchParams.get('start')).toMatch(/T07:00:00\+0[12]:00$/)
  })

  it('says so in the panel when the price cannot be loaded', async () => {
    stubApi(routes(() => availability(TWO_SPACES), () => new Response(null, { status: 500 })))
    renderPage()

    await check()

    expect(await screen.findByText(QUOTE_FAILED)).toBeDefined()
    expect(screen.getByRole('button', { name: 'Space A-02' })).toBeDefined()
  })

  it('shows a loading state while the lot loads', async () => {
    stubApi(routes())
    renderPage()

    expect(screen.getByRole('status').textContent).toBe('Loading lot…')
    expect(await screen.findByRole('heading', { name: 'Centar' })).toBeDefined()
  })

  it('shows the lot facts and links back to the lots section of the home page', async () => {
    stubApi(routes())
    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'Centar' })).toBeDefined()
    expect(screen.getByText('Ferhadija 1')).toBeDefined()
    expect(screen.getByText('2.50 BAM / hour')).toBeDefined()
    expect(screen.getByText('07:00–22:00')).toBeDefined()
    expect(screen.getByRole('link', { name: '← Back to parking lots' }).getAttribute('href')).toBe('/#locations')
  })

  it('says when the lot does not exist and links back to the lots', async () => {
    stubApi({})
    renderPage()

    expect((await screen.findByRole('alert')).textContent).toBe(LOT_NOT_FOUND)
    expect(screen.getByRole('link', { name: '← Back to parking lots' })).toBeDefined()
  })
})
