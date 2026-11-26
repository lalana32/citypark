import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { Link, MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTestQueryClient, problem, stubApi } from '../../test/apiStub'
import { SiteLayout } from './SiteLayout'

// jsdom has no scrollIntoView, scrollTo or matchMedia, so the tests record the calls on mocks.
const scrollIntoView = vi.fn()
const scrollTo = vi.fn()

function stubReducedMotion(reduce: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({ matches: reduce, media: query }))
}

beforeEach(() => {
  Element.prototype.scrollIntoView = scrollIntoView
  vi.stubGlobal('scrollTo', scrollTo)
  stubReducedMotion(false)
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  scrollIntoView.mockReset()
  scrollTo.mockReset()
})

function renderAt(path: string) {
  stubApi({ 'GET /api/v1/auth/me': () => problem(401) })
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<SiteLayout />}>
            <Route index element={<section id="how-it-works">Steps</section>} />
            <Route
              path="/lots"
              element={
                <>
                  <h1>Lots</h1>
                  <Link to="/lots?page=2">Next page</Link>
                </>
              }
            />
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
    expect(screen.getByRole('heading', { level: 1, name: 'Lots' })).toBeDefined()
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
    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth' })
  })

  it('jumps to the section when the user prefers reduced motion', () => {
    stubReducedMotion(true)
    renderAt('/lots')

    const nav = within(screen.getByRole('navigation', { name: 'Main' }))
    fireEvent.click(nav.getByRole('link', { name: 'How it works' }))

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'instant' })
  })

  it('opens a new page at the top', () => {
    renderAt('/')
    scrollTo.mockReset()

    const nav = within(screen.getByRole('navigation', { name: 'Main' }))
    fireEvent.click(nav.getByRole('link', { name: 'Parking lots' }))

    expect(scrollTo).toHaveBeenCalledWith({ top: 0, left: 0, behavior: 'instant' })
  })

  it('keeps the position when only the query changes', () => {
    renderAt('/lots')
    scrollTo.mockReset()

    fireEvent.click(screen.getByRole('link', { name: 'Next page' }))

    expect(scrollTo).not.toHaveBeenCalled()
  })

  it('does not scroll when the address has no section', () => {
    renderAt('/')

    expect(scrollIntoView).not.toHaveBeenCalled()
  })
})
