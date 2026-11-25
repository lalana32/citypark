import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTestQueryClient, problem, stubApi } from '../../test/apiStub'
import { SiteLayout } from './SiteLayout'

// jsdom has no scrollIntoView, so each test records the calls on its own mock.
const scrollIntoView = vi.fn()

beforeEach(() => {
  Element.prototype.scrollIntoView = scrollIntoView
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  scrollIntoView.mockReset()
})

function renderAt(path: string) {
  stubApi({ 'GET /api/v1/auth/me': () => problem(401) })
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<SiteLayout />}>
            <Route index element={<section id="how-it-works">Steps</section>} />
            <Route path="/lots" element={<h1>Lots</h1>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('site layout', () => {
  it('frames the page with the navigation and footer', () => {
    renderAt('/lots')

    expect(screen.getByRole('banner')).toBeDefined()
    expect(screen.getByRole('main').textContent).toBe('Lots')
    expect(screen.getByRole('contentinfo')).toBeDefined()
  })

  it('lays the navigation over the hero only on the home page', () => {
    renderAt('/')
    expect(screen.getByRole('banner').classList.contains('site-header-over-hero')).toBe(true)
    cleanup()

    renderAt('/lots')
    expect(screen.getByRole('banner').classList.contains('site-header-over-hero')).toBe(false)
  })

  it('opens the home page section from another page', () => {
    renderAt('/lots')

    const nav = within(screen.getByRole('navigation', { name: 'Main' }))
    fireEvent.click(nav.getByRole('link', { name: 'How it works' }))

    expect(screen.getByText('Steps')).toBeDefined()
    expect(scrollIntoView.mock.contexts).toEqual([document.getElementById('how-it-works')])
  })

  it('does not scroll when the address has no section', () => {
    renderAt('/')

    expect(scrollIntoView).not.toHaveBeenCalled()
  })
})
