import { describe, expect, it } from 'vitest'
import { billedHours, defaultSlot, formatSlotTime } from './slot'

describe('time slot', () => {
  it('starts at the next full hour and lasts two hours', () => {
    const slot = defaultSlot(new Date(2026, 10, 25, 13, 20))

    expect(slot.arrival).toEqual(new Date(2026, 10, 25, 14, 0))
    expect(slot.departure).toEqual(new Date(2026, 10, 25, 16, 0))
    expect(billedHours(slot)).toBe(2)
  })

  it('bills a started hour in full', () => {
    const slot = { arrival: new Date(2026, 10, 25, 14, 0), departure: new Date(2026, 10, 25, 15, 10) }

    expect(billedHours(slot)).toBe(2)
  })

  it('formats day, month and 24-hour time', () => {
    expect(formatSlotTime(new Date(2026, 10, 25, 9, 0))).toBe('25 Nov · 09:00')
  })
})
