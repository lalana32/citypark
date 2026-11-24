import { describe, expect, it } from 'vitest'
import { quarterHours, toLotInstant } from './interval'

describe('quarterHours', () => {
  it('lists every quarter hour of the window, both ends included', () => {
    expect(quarterHours('07:00:00', '08:00:00')).toEqual(['07:00', '07:15', '07:30', '07:45', '08:00'])
  })

  it('starts at the next grid time when the window opens off the grid', () => {
    expect(quarterHours('07:10', '07:45')).toEqual(['07:15', '07:30', '07:45'])
  })
})

describe('toLotInstant', () => {
  it('uses the winter offset of the lot', () => {
    expect(toLotInstant('2026-12-01', '10:00')).toBe('2026-12-01T10:00:00+01:00')
  })

  it('uses the summer offset of the lot', () => {
    expect(toLotInstant('2026-07-01', '10:00')).toBe('2026-07-01T10:00:00+02:00')
  })
})
