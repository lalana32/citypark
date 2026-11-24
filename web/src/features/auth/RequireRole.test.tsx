import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ADMIN, ANA, createTestQueryClient, problem, stubApi } from '../../test/apiStub'
import { RequireAuth } from './RequireAuth'
import { RequireRole } from './RequireRole'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderAdminRoute() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/login" element={<p>Login page</p>} />
          <Route element={<RequireAuth />}>
            <Route element={<RequireRole role="Admin" />}>
              <Route path="/admin" element={<p>Admin area</p>} />
            </Route>
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('RequireRole', () => {
  it('lets an admin open the page', async () => {
    stubApi({ 'GET /api/v1/auth/me': () => Response.json(ADMIN) })
    renderAdminRoute()

    expect(await screen.findByText('Admin area')).toBeDefined()
  })

  it('shows access denied to a customer', async () => {
    stubApi({ 'GET /api/v1/auth/me': () => Response.json(ANA) })
    renderAdminRoute()

    expect((await screen.findByRole('alert')).textContent).toBe('You do not have access to this page.')
    expect(screen.queryByText('Admin area')).toBeNull()
  })

  it('sends a signed-out visitor to the login page', async () => {
    stubApi({ 'GET /api/v1/auth/me': () => problem(401) })
    renderAdminRoute()

    expect(await screen.findByText('Login page')).toBeDefined()
    expect(screen.queryByText('Admin area')).toBeNull()
  })
})
