import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthPrompt } from './AuthPrompt'

afterEach(() => {
  cleanup()
})

const RETURN_TO = '/lots/1?date=2026-12-01&start=10:00&end=11:00&space=2'

function renderPrompt() {
  const onClose = vi.fn()
  render(
    <MemoryRouter>
      <AuthPrompt returnTo={RETURN_TO} onClose={onClose} />
    </MemoryRouter>,
  )
  return onClose
}

describe('auth prompt', () => {
  it('asks the guest to log in and carries the return target to both pages', () => {
    renderPrompt()

    const dialog = screen.getByRole('dialog', { name: 'Log in to continue' })
    expect(dialog.getAttribute('aria-modal')).toBe('true')
    const query = `?returnTo=${encodeURIComponent(RETURN_TO)}`
    expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe(`/login${query}`)
    expect(screen.getByRole('link', { name: 'Register' }).getAttribute('href')).toBe(`/register${query}`)
  })

  it('moves focus into the dialog', () => {
    renderPrompt()

    expect(document.activeElement).toBe(screen.getByRole('link', { name: 'Log in' }))
  })

  it('closes on Continue browsing and on Escape', () => {
    const onClose = renderPrompt()

    fireEvent.click(screen.getByRole('button', { name: 'Continue browsing' }))
    fireEvent.keyDown(document, { key: 'Escape' })

    expect(onClose).toHaveBeenCalledTimes(2)
  })
})
