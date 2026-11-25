import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ANA, createTestQueryClient, problem, stubApi } from '../../test/apiStub'
import { DEMO_LOTS } from './demoLots'
import { SpaceSelection } from './SpaceSelection'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const SLOT = { arrival: new Date(2026, 10, 25, 14, 0), departure: new Date(2026, 10, 25, 16, 0) }

function renderSelection(me: () => Response = () => problem(401)) {
  stubApi({ 'GET /api/v1/auth/me': me })
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <SpaceSelection lot={DEMO_LOTS[0]} slot={SLOT} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const panel = () => within(screen.getByRole('complementary', { name: 'Selected space' }))

describe('space selection', () => {
  it('keeps occupied spaces disabled and reserving off until a space is picked', () => {
    renderSelection()

    expect((screen.getByRole('button', { name: 'Space A-02, occupied' }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: 'Reserve space' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('selects a free space and shows the slot and estimated price', () => {
    renderSelection()

    fireEvent.click(screen.getByRole('button', { name: 'Space A-01, available' }))

    expect(screen.getByRole('button', { name: 'Space A-01, selected' }).getAttribute('aria-pressed')).toBe('true')
    expect(panel().getByText('A-01')).toBeDefined()
    expect(panel().getByText('Parking 01 · Level A')).toBeDefined()
    expect(panel().getByText('25 Nov · 14:00')).toBeDefined()
    expect(panel().getByText('25 Nov · 16:00')).toBeDefined()
    expect(panel().getByText('2 h')).toBeDefined()
    expect(panel().getByText('4.00 KM')).toBeDefined()
  })

  it('asks a guest to log in, and Continue browsing closes the prompt', () => {
    renderSelection()

    fireEvent.click(screen.getByRole('button', { name: 'Space A-03, available' }))
    const reserve = screen.getByRole('button', { name: 'Reserve space A-03' })
    fireEvent.click(reserve)

    expect(screen.getByRole('dialog', { name: 'Log in to continue' })).toBeDefined()

    fireEvent.click(screen.getByRole('button', { name: 'Continue browsing' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(reserve)
  })

  it('lets a signed-in user continue without the prompt', async () => {
    renderSelection(() => Response.json(ANA))

    fireEvent.click(screen.getByRole('button', { name: 'Space A-04, available' }))
    // Wait until the session has loaded: the guest-only note disappears.
    await vi.waitFor(() => expect(panel().queryByText('You only need to log in when you confirm.')).toBeNull())

    fireEvent.click(screen.getByRole('button', { name: 'Reserve space A-04' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.getByRole('status').textContent).toBe('Continuing to the reservation confirmation.')
  })
})
