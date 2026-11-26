import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { SiteFooter } from './SiteFooter'

afterEach(() => {
  cleanup()
})

describe('site footer', () => {
  it('holds the contact anchor, page navigation and the demo contact email', () => {
    render(
      <MemoryRouter>
        <SiteFooter />
      </MemoryRouter>,
    )

    expect(screen.getByRole('contentinfo').id).toBe('contact')

    const nav = within(screen.getByRole('navigation', { name: 'Footer' }))
    expect(nav.getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual([
      '/',
      '/lots',
      '/#how-it-works',
      '/#faq',
    ])
    expect(screen.getByRole('link', { name: 'contact@citypark.example' }).getAttribute('href')).toBe(
      'mailto:contact@citypark.example',
    )
    expect(screen.getByText(`© ${new Date().getFullYear()} CityPark · A demo project.`)).toBeDefined()
  })

  it('has no legal section or links to pages that do not exist', () => {
    render(
      <MemoryRouter>
        <SiteFooter />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('heading', { name: 'Legal' })).toBeNull()
    const hrefs = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(hrefs).not.toContain('/terms')
    expect(hrefs).not.toContain('/privacy')
  })
})
