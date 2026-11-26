import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTestQueryClient, page, problem, stubApi } from '../../test/apiStub'
import { HomePage } from './HomePage'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderHome() {
  stubApi({ 'GET /api/v1/auth/me': () => problem(401), 'GET /api/v1/lots': () => Response.json(page([])) })
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('home page', () => {
  it('shows the headline and links to the parking lots', () => {
    renderHome()

    expect(screen.getByRole('heading', { name: 'Stop circling. Start parking.' })).toBeDefined()
    expect(screen.getByRole('link', { name: 'Find parking' }).getAttribute('href')).toBe('/lots')
  })

  it('has the hero title as its only h1', () => {
    renderHome()

    expect(screen.getAllByRole('heading', { level: 1 }).map((heading) => heading.textContent)).toEqual([
      'Stop circling. Start parking.',
    ])
  })

  it('shows the hero button as text only', () => {
    renderHome()

    expect(screen.getByRole('link', { name: 'Find parking' }).querySelector('svg')).toBeNull()
  })

  it('sets the tab title', () => {
    renderHome()

    expect(document.title).toBe('Parking reservations · CityPark')
  })

  it('hides the decorative map from assistive technology', () => {
    const { container } = renderHome()

    expect(container.querySelector('.city-map-frame')?.getAttribute('aria-hidden')).toBe('true')
  })
})
