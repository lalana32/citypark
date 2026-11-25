import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LocationsSection } from './LocationsSection'

afterEach(() => {
  cleanup()
})

describe('locations section', () => {
  it('shows a card per lot with price and space counts', () => {
    render(<LocationsSection onViewLot={() => {}} />)

    const cards = within(screen.getByRole('list')).getAllByRole('listitem')
    expect(cards).toHaveLength(3)

    const first = within(cards[0])
    expect(first.getByRole('heading', { name: 'Parking 01' })).toBeDefined()
    expect(first.getByText('2.00 KM')).toBeDefined()
    expect(first.getByText('Total spaces').nextElementSibling?.textContent).toBe('12')
    expect(first.getByText('Free').nextElementSibling?.textContent).toBe('7')
  })

  it('jumps to the space selection with the chosen lot', () => {
    const onViewLot = vi.fn()
    render(<LocationsSection onViewLot={onViewLot} />)

    const view = screen.getByRole('link', { name: 'View parking: Parking 02' })
    fireEvent.click(view)

    expect(view.getAttribute('href')).toBe('#space-selection')
    expect(onViewLot).toHaveBeenCalledWith('parking-02')
  })
})
