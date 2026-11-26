import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoginPage, SESSION_ENDED } from '../auth/LoginPage'
import { RegisterPage } from '../auth/RegisterPage'
import { PLATE_OVERLAP, RESERVATION_LIMIT, SPACE_TAKEN } from '../reservations/api'
import {
  ANA,
  availabilityOf,
  CENTAR,
  createTestQueryClient,
  csrfRoute,
  type Handler,
  problem,
  type Route as ApiRoute,
  stubApi,
} from '../../test/apiStub'
import { LotAvailabilityPage, SPACE_GONE } from './LotAvailabilityPage'

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

/** The session, the lot, its availability (one answer per call, the last one repeats) and the booking answer. */
function routes(book: Handler, answers: { id: string; label: string }[][] = [[A02]], me: Handler = () => Response.json(ANA)) {
  let call = 0
  const result: Record<ApiRoute, Handler> = { ...csrfRoute, 'GET /api/v1/auth/me': me }
  result[`GET /api/v1/lots/${CENTAR.id}`] = () => Response.json(CENTAR)
  result[`GET /api/v1/lots/${CENTAR.id}/availability`] = () => availabilityOf(answers[Math.min(call++, answers.length - 1)])
  result['POST /api/v1/reservations'] = book
  return result
}

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[`/lots/${CENTAR.id}`]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
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
    expect(screen.getByRole('link', { name: 'My reservations' }).getAttribute('href')).toBe('/reservations')
    expect(screen.getByRole('link', { name: 'Back to home' }).getAttribute('href')).toBe('/')
    const [request] = calls(fetchMock, 'POST /api/v1/reservations')
    expect(await request.json()).toMatchObject({ spaceId: A02.id, plate: 'A12K345' })
  })

  it('locks the form while the booking is pending, so it is sent only once', async () => {
    let answer: (response: Response) => void = () => {}
    const fetchMock = stubApi(
      routes(
        () =>
          new Promise<Response>((resolve) => {
            answer = resolve
          }),
      ),
    )
    renderPage()

    await bookA02()

    const pending = await screen.findByRole('button', { name: 'Booking…' })
    expect(pending.closest('fieldset')?.disabled).toBe(true)
    fireEvent.click(pending)
    fireEvent.submit(pending.closest('form')!)
    answer(Response.json(RESERVATION, { status: 201 }))

    expect(await screen.findByRole('heading', { name: 'Booking confirmed' })).toBeDefined()
    expect(calls(fetchMock, 'POST /api/v1/reservations')).toHaveLength(1)
  })

  it('says the space was just taken and reloads the free spaces after a 409', async () => {
    const detail = `Space ${A02.id} already has an active reservation that overlaps the interval.`
    const fetchMock = stubApi(routes(() => problem(409, { detail, code: 'space_taken' }), [[A02, A03], [A03]]))
    renderPage()

    await bookA02()

    expect((await screen.findByRole('alert')).textContent).toBe(SPACE_TAKEN)
    await waitFor(() => {
      expect(screen.getByText('A-03')).toBeDefined()
      expect(screen.queryByText('A-02')).toBeNull()
    })
    expect(screen.queryByRole('button', { name: 'Confirm booking' })).toBeNull()
    expect(screen.getByText('Not chosen yet')).toBeDefined()
    expect(calls(fetchMock, `GET /api/v1/lots/${CENTAR.id}/availability`)).toHaveLength(2)
  })

  it('shows a plate booked elsewhere under the field and keeps the space after a 409', async () => {
    const fetchMock = stubApi(routes(() => problem(409, { code: 'plate_overlap' })))
    renderPage()

    await bookA02()

    const field = await screen.findByText(PLATE_OVERLAP)
    expect(screen.getByLabelText('License plate').getAttribute('aria-describedby')).toContain(field.id)
    expect(screen.getByRole('button', { name: 'Confirm booking' })).toBeDefined()
    expect(calls(fetchMock, `GET /api/v1/lots/${CENTAR.id}/availability`)).toHaveLength(1)
  })

  it('says the reservation limit is reached in the booking panel after a 409', async () => {
    stubApi(routes(() => problem(409, { code: 'reservation_limit' })))
    renderPage()

    await bookA02()

    expect((await screen.findByRole('alert')).textContent).toBe(RESERVATION_LIMIT)
    expect(screen.getByRole('button', { name: 'Confirm booking' })).toBeDefined()
  })

  it('shows an invalid plate under the field and keeps it editable after a 422', async () => {
    const message = "License plate 'X1' does not match the Bosnia and Herzegovina format, for example A12K345."
    stubApi(routes(() => problem(422, { detail: message, errors: { plate: [message] } })))
    renderPage()

    await bookA02('X1')

    const field = await screen.findByText(message)
    const input = screen.getByLabelText<HTMLInputElement>('License plate')
    expect(input.getAttribute('aria-describedby')).toContain(field.id)
    expect(input.getAttribute('aria-invalid')).toBe('true')
    expect(input.value).toBe('X1')
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('sends the user to sign in after a 401 and back to the lot afterwards', async () => {
    stubApi({
      ...routes(() => problem(401)),
      'POST /api/v1/auth/login': () => Response.json(ANA),
    })
    renderPage()

    await bookA02()

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeDefined()
    expect(screen.getByRole('status').textContent).toBe(SESSION_ENDED)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: ANA.email } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Secret123!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('heading', { name: 'Centar' })).toBeDefined()
  })
})

describe('reserving as a guest', () => {
  const asGuest = () => routes(() => Response.json(RESERVATION, { status: 201 }), [[A02]], () => problem(401))

  async function pickA02() {
    fireEvent.click(await screen.findByRole('button', { name: 'Check availability' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Space A-02' }))
  }

  it('asks a guest to sign in at Reserve instead of showing the plate field', async () => {
    stubApi(asGuest())
    renderPage()

    await pickA02()
    fireEvent.click(await screen.findByRole('button', { name: 'Reserve' }))

    expect(screen.getByRole('dialog', { name: 'Log in to continue' })).toBeDefined()
    expect(screen.queryByLabelText('License plate')).toBeNull()
    const returnTo = new URL(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')!, 'http://x').searchParams.get('returnTo')
    expect(returnTo).toContain(`/lots/${CENTAR.id}?`)
    expect(returnTo).toContain(`space=${A02.id}`)
  })

  it('closes the prompt on Continue browsing and keeps the chosen space', async () => {
    stubApi(asGuest())
    renderPage()

    await pickA02()
    fireEvent.click(await screen.findByRole('button', { name: 'Reserve' }))
    fireEvent.click(screen.getByRole('button', { name: 'Continue browsing' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.getByRole('button', { name: 'Space A-02' }).getAttribute('aria-pressed')).toBe('true')
  })
})

describe('coming back after signing in', () => {
  const asGuest = () => ({
    ...routes(() => Response.json(RESERVATION, { status: 201 }), [[A02]], () => problem(401)),
    'POST /api/v1/auth/login': () => Response.json(ANA),
    'POST /api/v1/auth/register': () => Response.json(ANA, { status: 201 }),
  })

  function fillCredentials() {
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: ANA.email } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Secret123!' } })
  }

  async function reserveA02() {
    fireEvent.click(await screen.findByRole('button', { name: 'Check availability' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Space A-02' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Reserve' }))
  }

  it('says the space is no longer free after login and lets the user pick again', async () => {
    // A02 is free while the guest picks it and taken by the time they are back.
    stubApi({
      ...routes(() => problem(500), [[A02, A03], [A03]], () => problem(401)),
      'POST /api/v1/auth/login': () => Response.json(ANA),
    })
    renderPage()

    await reserveA02()
    fireEvent.click(screen.getByRole('link', { name: 'Log in' }))
    fillCredentials()
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in' }))

    expect((await screen.findByRole('alert')).textContent).toBe(SPACE_GONE)
    expect(screen.queryByRole('button', { name: 'Space A-02' })).toBeNull()
    expect(screen.queryByLabelText('License plate')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Space A-03' }))
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.getByLabelText('License plate')).toBeDefined()
  })

  async function expectBackOnA02() {
    expect(await screen.findByRole('heading', { name: 'Centar' })).toBeDefined()
    expect((await screen.findByRole('button', { name: 'Space A-02' })).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByLabelText('License plate')).toBeDefined()
  }

  it('brings the guest back to the same time and space after login, ready to confirm', async () => {
    stubApi(asGuest())
    renderPage()

    await reserveA02()
    fireEvent.click(screen.getByRole('link', { name: 'Log in' }))
    fillCredentials()
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in' }))

    await expectBackOnA02()
  })

  it('brings a new user back after register and then login', async () => {
    stubApi(asGuest())
    renderPage()

    await reserveA02()
    fireEvent.click(screen.getByRole('link', { name: 'Register' }))
    fillCredentials()
    fireEvent.click(await screen.findByRole('button', { name: 'Create account' }))
    expect(await screen.findByText('Account created. Sign in to continue.')).toBeDefined()
    fillCredentials()
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    await expectBackOnA02()
  })
})
