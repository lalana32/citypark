import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CENTAR, createTestQueryClient, problem, type Route as ApiRoute, stubApi } from '../../test/apiStub'
import { LOT_NOT_FOUND } from './api'
import { LotAvailabilityPage } from './LotAvailabilityPage'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

/** The lot route, plus the availability route when it has an answer. */
function routes(respond?: () => Response): Record<ApiRoute, () => Response> {
  const result: Record<ApiRoute, () => Response> = {}
  result[`GET /api/v1/lots/${CENTAR.id}`] = () => Response.json(CENTAR)
  if (respond) {
    result[`GET /api/v1/lots/${CENTAR.id}/availability`] = respond
  }
  return result
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
    expect(screen.getByText('A-02')).toBeDefined()
    const request = fetchMock.mock.calls.at(-1)![0] as Request
    const query = new URL(request.url).searchParams
    expect(query.get('start')).toMatch(/T07:00:00\+0[12]:00$/)
    expect(query.get('end')).toMatch(/T08:00:00\+0[12]:00$/)
  })

  it('says so clearly when no space is free', async () => {
    stubApi(routes(() => availability([])))
    renderPage()

    await check()

    expect(await screen.findByText('No free spaces for this interval. Try another time.')).toBeDefined()
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
