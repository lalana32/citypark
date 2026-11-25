import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ANA, createTestQueryClient, csrfRoute, problem, stubApi } from '../../test/apiStub'
import { INVALID_CREDENTIALS } from './authErrors'
import { LoginPage } from './LoginPage'
import { RequireAuth } from './RequireAuth'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderApp(path: string) {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<RequireAuth />}>
            <Route path="/" element={<p>Home</p>} />
            <Route path="/reservations" element={<p>My reservations</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function signIn(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('LoginPage', () => {
  it('signs in and goes to the home page', async () => {
    stubApi({ ...csrfRoute, 'POST /api/v1/auth/login': () => Response.json(ANA) })
    renderApp('/login')

    signIn(ANA.email, 'Secret123!')

    expect(await screen.findByText('Home')).toBeDefined()
  })

  it('shows a generic error on a wrong password', async () => {
    stubApi({ ...csrfRoute, 'POST /api/v1/auth/login': () => problem(401, { detail: 'Account is locked.' }) })
    renderApp('/login')

    signIn(ANA.email, 'wrong')

    expect((await screen.findByRole('alert')).textContent).toBe(INVALID_CREDENTIALS)
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined()
  })

  it('tells the user to wait after too many attempts', async () => {
    stubApi({ ...csrfRoute, 'POST /api/v1/auth/login': () => problem(429, {}, { 'Retry-After': '30' }) })
    renderApp('/login')

    signIn(ANA.email, 'wrong')

    expect((await screen.findByRole('alert')).textContent).toBe('Too many attempts. Try again in 30 seconds.')
  })

  it('shows a server field error under its field', async () => {
    stubApi({ ...csrfRoute, 'POST /api/v1/auth/login': () => problem(400, { errors: { email: ['Email is too long.'] } }) })
    renderApp('/login')

    signIn('a@b.c', 'x')

    expect(await screen.findByText('Email is too long.')).toBeDefined()
    expect(screen.getByLabelText('Email').getAttribute('aria-invalid')).toBe('true')
  })

  it('checks required fields without calling the API', async () => {
    const fetchMock = stubApi({})
    renderApp('/login')

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Email is required.')).toBeDefined()
    expect(screen.getByText('Password is required.')).toBeDefined()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('return target', () => {
  it('goes back to the in-app return target after signing in', async () => {
    stubApi({ ...csrfRoute, 'GET /api/v1/auth/me': () => Response.json(ANA), 'POST /api/v1/auth/login': () => Response.json(ANA) })
    renderApp(`/login?returnTo=${encodeURIComponent('/reservations?page=2')}`)

    signIn(ANA.email, 'Secret123!')

    expect(await screen.findByText('My reservations')).toBeDefined()
  })

  it('ignores an external return target and goes home', async () => {
    stubApi({ ...csrfRoute, 'GET /api/v1/auth/me': () => Response.json(ANA), 'POST /api/v1/auth/login': () => Response.json(ANA) })
    renderApp(`/login?returnTo=${encodeURIComponent('//evil.com/phish')}`)

    signIn(ANA.email, 'Secret123!')

    expect(await screen.findByText('Home')).toBeDefined()
  })

  it('keeps the return target on the link to registration', () => {
    stubApi({})
    renderApp(`/login?returnTo=${encodeURIComponent('/reservations')}`)

    expect(screen.getByRole('link', { name: 'Create one' }).getAttribute('href')).toBe('/register?returnTo=%2Freservations')
  })
})

describe('RequireAuth', () => {
  it('shows the page when the cookie session is valid', async () => {
    stubApi({ 'GET /api/v1/auth/me': () => Response.json(ANA) })
    renderApp('/reservations')

    expect(await screen.findByText('My reservations')).toBeDefined()
  })

  it('sends a signed-out user to login and back to the page they wanted', async () => {
    stubApi({
      ...csrfRoute,
      'GET /api/v1/auth/me': () => problem(401),
      'POST /api/v1/auth/login': () => Response.json(ANA),
    })
    renderApp('/reservations')

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeDefined()
    signIn(ANA.email, 'Secret123!')

    expect(await screen.findByText('My reservations')).toBeDefined()
  })
})
