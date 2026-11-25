import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { FaqSection } from './FaqSection'

afterEach(() => {
  cleanup()
})

describe('faq', () => {
  it('starts with the first answer open', () => {
    render(<FaqSection />)

    expect(screen.getAllByRole('button')).toHaveLength(4)
    expect(screen.getByRole('button', { name: 'Can I check availability without registering?' }).getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByRole('region', { name: 'Can I check availability without registering?' })).toBeDefined()
  })

  it('opens and closes an answer', () => {
    render(<FaqSection />)
    const question = screen.getByRole('button', { name: 'Can I cancel a reservation?' })
    expect(screen.queryByRole('region', { name: 'Can I cancel a reservation?' })).toBeNull()

    fireEvent.click(question)
    expect(question.getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByRole('region', { name: 'Can I cancel a reservation?' })).toBeDefined()

    fireEvent.click(question)
    expect(question.getAttribute('aria-expanded')).toBe('false')
    expect(screen.queryByRole('region', { name: 'Can I cancel a reservation?' })).toBeNull()
  })
})
