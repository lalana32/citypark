import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ParkingPlan } from './ParkingPlan'
import { aisleCount, type PlanSpace, planHeight, SPACE_H, SPACE_W, spaceBox, VIEW_W } from './planLayout'

afterEach(cleanup)

const spaces: PlanSpace[] = [
  { id: '1', label: 'A-01', state: 'free' },
  { id: '2', label: 'A-02', state: 'occupied' },
  { id: '3', label: 'A-03', state: 'selected' },
]

describe('ParkingPlan', () => {
  it('describes the plan for screen readers', () => {
    render(<ParkingPlan title="Centar" spaces={spaces} />)

    expect(screen.getByRole('img', { name: 'Plan of Centar: 3 spaces, 2 available, space A-03 selected' })).toBeDefined()
  })

  it('draws every space with its label and marks only the free ones as available', () => {
    render(<ParkingPlan title="Centar" spaces={spaces} />)

    for (const space of spaces) {
      expect(screen.getByText(space.label)).toBeDefined()
    }
    // Two free spaces plus the legend entry.
    expect(screen.getAllByText('Available')).toHaveLength(3)
  })

  it('has no buttons without onSelect, as on the home page', () => {
    render(<ParkingPlan title="Centar" spaces={spaces} />)

    expect(screen.queryAllByRole('button')).toHaveLength(0)
  })

  it('makes only the free spaces buttons, named by label, with the selected one pressed', () => {
    render(<ParkingPlan title="Centar" spaces={spaces} onSelect={() => {}} />)

    expect(screen.getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual([
      'Space A-01',
      'Space A-03',
    ])
    expect(screen.queryByRole('button', { name: 'Space A-02' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Space A-01', pressed: false })).toBeDefined()
    expect(screen.getByRole('button', { name: 'Space A-03', pressed: true })).toBeDefined()
  })

  it('reports the id of the clicked space', () => {
    const onSelect = vi.fn()
    render(<ParkingPlan title="Centar" spaces={spaces} onSelect={onSelect} />)

    fireEvent.click(screen.getByRole('button', { name: 'Space A-01' }))

    expect(onSelect).toHaveBeenCalledWith('1')
  })

  it.each([12, 20, 30])('draws a lot of %i spaces completely, inside the drawing', (count) => {
    // Every third space is occupied.
    const lot: PlanSpace[] = Array.from({ length: count }, (_, index) => {
      const label = `A-${String(index + 1).padStart(2, '0')}`
      return { id: label, label, state: index % 3 === 0 ? 'occupied' : 'free' }
    })
    render(<ParkingPlan title="Centar" spaces={lot} onSelect={() => {}} />)

    for (const space of lot) {
      expect(screen.getByText(space.label)).toBeDefined()
    }
    expect(screen.getAllByRole('button')).toHaveLength(lot.filter((space) => space.state === 'free').length)
    lot.forEach((_, index) => {
      const { x, y } = spaceBox(index)
      expect(x + SPACE_W).toBeLessThanOrEqual(VIEW_W)
      expect(y + SPACE_H).toBeLessThanOrEqual(planHeight(count))
    })
  })

  it('puts each button on its space in percent of the drawing', () => {
    render(<ParkingPlan title="Centar" spaces={spaces} onSelect={() => {}} />)

    // A-03 is the third space: x 514 of 1400, y 184 of 960.
    const button = screen.getByRole('button', { name: 'Space A-03' })
    // Compared as numbers: the CSS parser may round the value it reads back.
    expect(button.style.left).toMatch(/%$/)
    expect(parseFloat(button.style.left)).toBeCloseTo((514 / 1400) * 100, 3)
    expect(parseFloat(button.style.top)).toBeCloseTo((184 / 960) * 100, 3)
  })
})

describe('plan layout', () => {
  it('keeps the original size for one aisle of 12 spaces', () => {
    expect(aisleCount(12)).toBe(1)
    expect(planHeight(12)).toBe(960)
  })

  it('adds an aisle for every 12 spaces', () => {
    expect(aisleCount(30)).toBe(3)
    expect(planHeight(30)).toBeGreaterThan(planHeight(24))
  })

  it('puts six spaces above the lane, six below, then starts the next aisle', () => {
    expect(spaceBox(5)).toMatchObject({ top: true, x: 1114 })
    expect(spaceBox(6)).toMatchObject({ top: false, x: 114 })
    expect(spaceBox(12)).toMatchObject({ top: true, x: 114 })
    expect(spaceBox(12).y).toBeGreaterThan(spaceBox(11).y)
  })
})
