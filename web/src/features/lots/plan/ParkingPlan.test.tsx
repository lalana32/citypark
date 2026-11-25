import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ParkingPlan } from './ParkingPlan'
import { aisleCount, type PlanSpace, planHeight, spaceBox } from './planLayout'

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
