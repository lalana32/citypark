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
const accountMenu = () => document.getElementById('site-account-menu')

describe('site navigation', () => {
  it('offers a visitor the page sections, log in and register', () => {
    renderNav(() => problem(401))

    const links = mainNav()
      .getAllByRole('link')
      .map((link) => [link.textContent, link.getAttribute('href')])
    expect(links).toEqual([
      ['Home', '/'],
      ['Parking lots', '/lots'],
      ['How it works', '/#how-it-works'],
      ['Contact', '/#contact'],
    ])
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
    const menu = within(accountMenu()!)
    expect(menu.getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual(['/reservations'])
    expect(menu.getByRole('button', { name: 'Log out' })).toBeDefined()

    fireEvent.keyDown(document, { key: 'Escape' })
    expect(accountMenu()).toBeNull()
  })

  it('links a signed-in user only to pages that exist on mobile', async () => {
    renderNav(() => Response.json(ANA))

    await screen.findByRole('button', { name: 'My account' })
    fireEvent.click(screen.getByRole('button', { name: 'Menu' }))

    const mobileNav = within(screen.getByRole('navigation', { name: 'Main mobile' }))
    expect(mobileNav.getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual([
      '/',
      '/lots',
      '/reservations',
    ])
    expect(mobileNav.getByRole('button', { name: 'Log out' })).toBeDefined()
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
