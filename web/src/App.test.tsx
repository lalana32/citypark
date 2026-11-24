import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { ANA, createTestQueryClient, csrfRoute, problem, stubApi } from './test/apiStub'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const signedOut = { 'GET /api/v1/auth/me': () => problem(401) }
const signedIn = { 'GET /api/v1/auth/me': () => Response.json(ANA) }

function renderApp(path: string) {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function register(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Create account' }))
}

describe('session', () => {
  it('keeps the user signed in on a fresh load when the cookie is valid', async () => {
    stubApi(signedIn)
    renderApp('/')

    expect(await screen.findByText('Parking reservations.')).toBeDefined()
    expect(screen.getByText(ANA.email)).toBeDefined()
  })

  it('logs out and returns to the login screen', async () => {
    stubApi({ ...csrfRoute, ...signedIn, 'POST /api/v1/auth/logout': () => new Response(null, { status: 204 }) })
    renderApp('/')

    fireEvent.click(await screen.findByRole('button', { name: 'Log out' }))

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeDefined()
    expect(screen.queryByText(ANA.email)).toBeNull()
  })
})

describe('registration', () => {
  it('creates the account and asks the user to sign in', async () => {
    stubApi({ ...csrfRoute, ...signedOut, 'POST /api/v1/auth/register': () => Response.json(ANA, { status: 201 }) })
    renderApp('/register')

    register(ANA.email, 'Secret123!')

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeDefined()
    expect(screen.getByRole('status').textContent).toBe('Account created. Sign in to continue.')
  })

  it('shows password rules under the password field', async () => {
    const errors = { password: ['Passwords must have at least one digit.'] }
    stubApi({ ...csrfRoute, ...signedOut, 'POST /api/v1/auth/register': () => problem(422, { errors }) })
    renderApp('/register')

    register(ANA.email, 'NoDigits!')

    expect(await screen.findByText('Passwords must have at least one digit.')).toBeDefined()
    expect(screen.getByLabelText('Password').getAttribute('aria-invalid')).toBe('true')
  })

  it('shows the conflict when the email is taken', async () => {
    const detail = 'An account with this email already exists.'
    stubApi({ ...csrfRoute, ...signedOut, 'POST /api/v1/auth/register': () => problem(409, { detail }) })
    renderApp('/register')

    register(ANA.email, 'Secret123!')

    expect((await screen.findByRole('alert')).textContent).toBe(detail)
  })
})
