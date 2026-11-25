import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTestQueryClient, problem, stubApi } from '../../test/apiStub'
import { HomePage } from './HomePage'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderHome() {
  stubApi({ 'GET /api/v1/auth/me': () => problem(401) })
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

  it('opens the chosen lot on the plan and clears the old selection', () => {
    renderHome()

    fireEvent.click(screen.getByRole('button', { name: 'Space A-01, available' }))
    fireEvent.click(screen.getByRole('link', { name: 'View parking: Parking 02' }))

    const panel = within(screen.getByRole('complementary', { name: 'Selected space' }))
    expect(panel.getByText('Parking 02 · Level A')).toBeDefined()
    expect(screen.getByRole('button', { name: 'Reserve space' })).toBeDefined()
  })

  it('hides the decorative map from assistive technology', () => {
    const { container } = renderHome()

    expect(container.querySelector('.city-map-frame')?.getAttribute('aria-hidden')).toBe('true')
  })
})
