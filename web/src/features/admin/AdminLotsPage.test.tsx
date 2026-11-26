import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import { ADMIN, ANA, CENTAR, createTestQueryClient, csrfRoute, page, problem, type Route as ApiRoute, stubApi } from '../../test/apiStub'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const CREATE = 'POST /api/v1/admin/lots'
const UPDATE = `PUT /api/v1/admin/lots/${CENTAR.id}`
const LIST = 'GET /api/v1/lots'
const ADD_SPACE = `POST /api/v1/admin/lots/${CENTAR.id}/spaces`

function routes(user: object, extra: Record<ApiRoute, () => Response> = {}) {
  const result: Record<ApiRoute, () => Response> = {
    ...csrfRoute,
    'GET /api/v1/auth/me': () => Response.json(user),
    [LIST]: () => Response.json(page([CENTAR])),
  }
  return { ...result, ...extra }
}

function renderApp(path = '/admin/lots') {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function calls(fetchMock: ReturnType<typeof stubApi>, route: string) {
  return fetchMock.mock.calls
    .map(([input]) => (input instanceof Request ? input : new Request(input)))
    .filter((request) => `${request.method} ${new URL(request.url).pathname}` === route)
}

function type(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } })
}

describe('admin access', () => {
  it('shows the Admin link to an admin only', async () => {
    stubApi(routes(ADMIN))
    renderApp('/lots')

    expect((await screen.findByRole('link', { name: 'Admin' })).getAttribute('href')).toBe('/admin/lots')
  })

  it('keeps a customer out of the admin pages', async () => {
    stubApi(routes(ANA))
    renderApp()

    expect((await screen.findByRole('alert')).textContent).toBe('You do not have access to this page.')
    expect(screen.queryByRole('heading', { name: 'Manage parking lots' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Admin' })).toBeNull()
  })
})

describe('admin lots', () => {
  it('sets the tab title', async () => {
    stubApi(routes(ADMIN))
    renderApp()

    expect(await screen.findByRole('heading', { name: 'Manage parking lots' })).toBeDefined()
    expect(document.title).toBe('Manage parking lots · CityPark')
  })

  it('creates a lot and reloads the list', async () => {
    const created = { ...CENTAR, id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d02', name: 'Marijin Dvor', totalSpaces: 0 }
    const fetchMock = stubApi(routes(ADMIN, { [CREATE]: () => Response.json(created, { status: 201 }) }))
    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: 'New lot' }))
    type('Name', 'Marijin Dvor')
    type('Address', 'Zmaja od Bosne 8')
    type('Hourly price', '3')
    fireEvent.click(screen.getByRole('button', { name: 'Create lot' }))

    expect((await screen.findByRole('status')).textContent).toBe('Lot Marijin Dvor saved.')
    const [request] = calls(fetchMock, CREATE)
    expect(await request.json()).toEqual({
      name: 'Marijin Dvor',
      address: 'Zmaja od Bosne 8',
      hourlyPrice: 3,
      currency: 'BAM',
      opensAt: '07:00:00',
      closesAt: '22:00:00',
    })
    await vi.waitFor(() => expect(calls(fetchMock, LIST)).toHaveLength(2))
  })

  it('edits the rate of an existing lot', async () => {
    const fetchMock = stubApi(routes(ADMIN, { [UPDATE]: () => Response.json({ ...CENTAR, hourlyPrice: 3 }) }))
    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: 'Edit Centar' }))
    expect(screen.getByLabelText<HTMLInputElement>('Hourly price').value).toBe('2.5')
    type('Hourly price', '3')
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect((await screen.findByRole('status')).textContent).toBe('Lot Centar saved.')
    const [request] = calls(fetchMock, UPDATE)
    expect(await request.json()).toMatchObject({ hourlyPrice: 3, opensAt: '07:00:00', closesAt: '22:00:00' })
  })

  it('shows a 422 rule message next to its field', async () => {
    const message = 'ClosesAt (07:00) must be after OpensAt (22:00).'
    stubApi(routes(ADMIN, { [CREATE]: () => problem(422, { detail: message, errors: { closesAt: [message] } }) }))
    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: 'New lot' }))
    type('Name', 'Noćni')
    type('Address', 'Titova 1')
    type('Hourly price', '2')
    type('Opens at', '22:00')
    type('Closes at', '07:00')
    fireEvent.click(screen.getByRole('button', { name: 'Create lot' }))

    expect(await screen.findByText(message)).toBeDefined()
    expect(screen.getByLabelText('Closes at').getAttribute('aria-invalid')).toBe('true')
  })
})

describe('admin spaces', () => {
  it('adds a space, clears the field for the next one and reloads the lots', async () => {
    const space = { id: '019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e31', label: 'B-01', isActive: true }
    const fetchMock = stubApi(routes(ADMIN, { [ADD_SPACE]: () => Response.json(space, { status: 201 }) }))
    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: 'Add a space to Centar' }))
    type('Label', 'b-01')
    fireEvent.click(screen.getByRole('button', { name: 'Add space' }))

    expect((await screen.findByRole('status')).textContent).toBe('Space B-01 added to Centar.')
    expect(await calls(fetchMock, ADD_SPACE)[0].json()).toEqual({ label: 'b-01' })
    await vi.waitFor(() => expect(screen.getByLabelText<HTMLInputElement>('Label').value).toBe(''))
    await vi.waitFor(() => expect(calls(fetchMock, LIST)).toHaveLength(2))
  })

  it('shows a duplicate label next to the field', async () => {
    const detail = "Lot 'Centar' already has a space labelled 'B-01'."
    stubApi(routes(ADMIN, { [ADD_SPACE]: () => problem(409, { detail }) }))
    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: 'Add a space to Centar' }))
    type('Label', 'B-01')
    fireEvent.click(screen.getByRole('button', { name: 'Add space' }))

    expect(await screen.findByText(detail)).toBeDefined()
    expect(screen.getByLabelText('Label').getAttribute('aria-invalid')).toBe('true')
  })
})
