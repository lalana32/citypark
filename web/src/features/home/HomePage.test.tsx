import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { HomePage } from './HomePage'

afterEach(() => {
  cleanup()
})

function renderHome() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  )
}

describe('home page', () => {
  it('shows the headline and links to the parking lots', () => {
    renderHome()

    expect(screen.getByRole('heading', { name: 'Stop circling. Start parking.' })).toBeDefined()
    expect(screen.getByRole('link', { name: 'Find parking' }).getAttribute('href')).toBe('/lots')
  })

  it('hides the decorative map from assistive technology', () => {
    const { container } = renderHome()

    expect(container.querySelector('.city-map-frame')?.getAttribute('aria-hidden')).toBe('true')
  })
})
