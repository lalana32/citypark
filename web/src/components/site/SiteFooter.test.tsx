import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { SiteFooter } from './SiteFooter'

afterEach(() => {
  cleanup()
})

describe('site footer', () => {
  it('holds the contact anchor, page navigation and legal links', () => {
    render(
      <MemoryRouter>
        <SiteFooter />
      </MemoryRouter>,
    )

    expect(screen.getByRole('contentinfo').id).toBe('contact')

    const nav = within(screen.getByRole('navigation', { name: 'Footer' }))
    expect(nav.getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual([
      '/',
      '/#locations',
      '/#how-it-works',
      '/#faq',
    ])
    expect(screen.getByRole('link', { name: 'Terms of use' }).getAttribute('href')).toBe('/terms')
    expect(screen.getByRole('link', { name: 'Privacy policy' }).getAttribute('href')).toBe('/privacy')
    expect(screen.getByText(`© ${new Date().getFullYear()} CityPark. All rights reserved.`)).toBeDefined()
  })
})
