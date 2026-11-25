import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from '../auth/LoginPage'
import { SPACE_TAKEN } from '../reservations/api'
import { ANA, CENTAR, createTestQueryClient, csrfRoute, problem, type Route as ApiRoute, stubApi } from '../../test/apiStub'
import { LotAvailabilityPage } from './LotAvailabilityPage'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const A02 = { id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02', label: 'A-02' }
const A03 = { id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e03', label: 'A-03' }

const RESERVATION = {
  id: '019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01',
  lotName: 'Centar',
  spaceLabel: 'A-02',
  start: '2026-12-01T09:00:00+00:00',
  end: '2026-12-01T10:30:00+00:00',
  plate: 'A12K345',
  price: 5,
  currency: 'BAM',
  state: 'Upcoming',
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

/** The lot, its availability (one answer per call, the last one repeats) and the booking answer. */
function routes(book: () => Response, answers: { id: string; label: string }[][] = [[A02]]) {
  let call = 0
  const result: Record<ApiRoute, () => Response> = { ...csrfRoute }
  result[`GET /api/v1/lots/${CENTAR.id}`] = () => Response.json(CENTAR)
  result[`GET /api/v1/lots/${CENTAR.id}/availability`] = () => availability(answers[Math.min(call++, answers.length - 1)])
  result['POST /api/v1/reservations'] = book
  return result
}

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[`/lots/${CENTAR.id}`]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/lots/:id" element={<LotAvailabilityPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function bookA02(plate = 'A12K345') {
  fireEvent.click(await screen.findByRole('button', { name: 'Check availability' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Space A-02' }))
  fireEvent.change(screen.getByLabelText('License plate'), { target: { value: plate } })
  fireEvent.click(screen.getByRole('button', { name: 'Continue' }))
  fireEvent.click(screen.getByRole('button', { name: 'Confirm booking' }))
}

function calls(fetchMock: ReturnType<typeof stubApi>, route: string) {
  return fetchMock.mock.calls
    // The CSRF call passes a URL, every API call a Request.
    .map(([input]) => (input instanceof Request ? input : new Request(input)))
    .filter((request) => `${request.method} ${new URL(request.url).pathname}` === route)
}

describe('booking a space', () => {
  it('shows the price and the times in lot time after a 201', async () => {
    const fetchMock = stubApi(routes(() => Response.json(RESERVATION, { status: 201 })))
    renderPage()

    await bookA02()

    expect(await screen.findByRole('heading', { name: 'Booking confirmed' })).toBeDefined()
    expect(screen.getByText('1 Dec 2026, 10:00')).toBeDefined()
    expect(screen.getByText('1 Dec 2026, 11:30')).toBeDefined()
    expect(screen.getByText('5.00 BAM')).toBeDefined()
    const [request] = calls(fetchMock, 'POST /api/v1/reservations')
    expect(await request.json()).toMatchObject({ spaceId: A02.id, plate: 'A12K345' })
  })

  it('says the space was just taken and reloads the free spaces after a 409', async () => {
    const detail = `Space ${A02.id} already has an active reservation that overlaps the interval.`
    const fetchMock = stubApi(routes(() => problem(409, { detail }), [[A02, A03], [A03]]))
    renderPage()

    await bookA02()

    expect((await screen.findByRole('alert')).textContent).toBe(SPACE_TAKEN)
    await waitFor(() => {
      expect(screen.getByText('A-03')).toBeDefined()
      expect(screen.queryByText('A-02')).toBeNull()
    })
    expect(screen.queryByRole('button', { name: 'Confirm booking' })).toBeNull()
    expect(calls(fetchMock, `GET /api/v1/lots/${CENTAR.id}/availability`)).toHaveLength(2)
  })

  it('shows the rule message and keeps the plate editable after a 422', async () => {
    const message = "License plate 'X1' does not match the Bosnia and Herzegovina format, for example A12K345."
    stubApi(routes(() => problem(422, { detail: message, errors: { general: [message] } })))
    renderPage()

    await bookA02('X1')

    expect((await screen.findByRole('alert')).textContent).toBe(message)
    expect(screen.getByLabelText<HTMLInputElement>('License plate').value).toBe('X1')
  })

  it('sends the user to sign in after a 401 and back to the lot afterwards', async () => {
    stubApi({
      ...routes(() => problem(401)),
      'POST /api/v1/auth/login': () => Response.json(ANA),
    })
    renderPage()

    await bookA02()

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeDefined()
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: ANA.email } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Secret123!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('heading', { name: 'Centar' })).toBeDefined()
  })
})
