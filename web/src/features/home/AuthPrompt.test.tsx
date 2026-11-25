import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthPrompt } from './AuthPrompt'

afterEach(() => {
  cleanup()
})

function renderPrompt() {
  const onClose = vi.fn()
  render(
    <MemoryRouter>
      <AuthPrompt onClose={onClose} />
    </MemoryRouter>,
  )
  return onClose
}

describe('auth prompt', () => {
  it('asks the guest to log in and links to the existing pages', () => {
    renderPrompt()

    const dialog = screen.getByRole('dialog', { name: 'Log in to continue' })
    expect(dialog.getAttribute('aria-modal')).toBe('true')
    expect(screen.getByText('To reserve a parking space, you need to log in to your account.')).toBeDefined()
    expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login')
    expect(screen.getByRole('link', { name: 'Register' }).getAttribute('href')).toBe('/register')
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
