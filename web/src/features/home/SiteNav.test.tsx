import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ADMIN, ANA, createTestQueryClient, problem, stubApi } from '../../test/apiStub'
import { SiteNav } from './SiteNav'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderNav(me: () => Response) {
  stubApi({ 'GET /api/v1/auth/me': me })
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <SiteNav />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const mainNav = () => within(screen.getByRole('navigation', { name: 'Main' }))

describe('site navigation', () => {
  it('offers a visitor the page sections, log in and register', () => {
    renderNav(() => problem(401))

    const names = mainNav()
      .getAllByRole('link')
      .map((link) => link.textContent)
    expect(names).toEqual(['Home', 'Parking lots', 'How it works', 'Contact'])
    expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login')
    expect(screen.getAllByRole('link', { name: 'Register' })[0].getAttribute('href')).toBe('/register')
    expect(screen.queryByRole('button', { name: 'My account' })).toBeNull()
  })

  it('gives a signed-in user their reservations and an account menu', async () => {
    renderNav(() => Response.json(ANA))

    const account = await screen.findByRole('button', { name: 'My account' })
    expect(mainNav().getByRole('link', { name: 'My reservations' }).getAttribute('href')).toBe('/reservations')
    expect(mainNav().queryByRole('link', { name: 'Contact' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull()
    expect(mainNav().queryByRole('link', { name: 'Admin' })).toBeNull()

    fireEvent.click(account)
    expect(account.getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByRole('link', { name: 'Account overview' })).toBeDefined()
    expect(screen.getByRole('link', { name: 'Profile' })).toBeDefined()
    expect(screen.getByRole('button', { name: 'Log out' })).toBeDefined()

    fireEvent.keyDown(document, { key: 'Escape' })
    expect(screen.queryByRole('link', { name: 'Account overview' })).toBeNull()
  })

  it('keeps the admin link for admins', async () => {
    renderNav(() => Response.json(ADMIN))

    await screen.findByRole('button', { name: 'My account' })
    expect(mainNav().getByRole('link', { name: 'Admin' }).getAttribute('href')).toBe('/admin/lots')
  })

  it('opens and closes the mobile menu', () => {
    renderNav(() => problem(401))

    const toggle = screen.getByRole('button', { name: 'Menu' })
    expect(toggle.getAttribute('aria-expanded')).toBe('false')

    fireEvent.click(toggle)
    const mobileNav = screen.getByRole('navigation', { name: 'Main mobile' })
    expect(toggle.getAttribute('aria-expanded')).toBe('true')

    fireEvent.click(within(mobileNav).getByRole('link', { name: 'How it works' }))
    expect(screen.queryByRole('navigation', { name: 'Main mobile' })).toBeNull()
  })
})
