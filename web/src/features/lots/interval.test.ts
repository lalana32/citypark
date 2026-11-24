import { describe, expect, it } from 'vitest'
import { formatLotDateTime, quarterHours, toLotInstant } from './interval'

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

describe('formatLotDateTime', () => {
  it('shows a winter UTC instant in lot time', () => {
    expect(formatLotDateTime('2026-12-01T09:00:00+00:00')).toBe('1 Dec 2026, 10:00')
  })

  it('shows a summer UTC instant in lot time', () => {
    expect(formatLotDateTime('2026-07-01T08:30:00+00:00')).toBe('1 Jul 2026, 10:30')
  })

  it('moves to the next day when lot time is already past midnight', () => {
    expect(formatLotDateTime('2026-12-01T23:30:00+00:00')).toBe('2 Dec 2026, 00:30')
  })
})
