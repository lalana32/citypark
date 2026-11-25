import { describe, expect, it } from 'vitest'
import { formatDate, formatDuration, formatLotDateTime, isOpenFor, nextSlot, quarterHours, toLotInstant } from './interval'

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

describe('nextSlot', () => {
  it('starts at the next quarter hour on the lot clock and lasts two hours', () => {
    expect(nextSlot(new Date('2026-12-01T09:07:00Z'))).toEqual({ date: '2026-12-01', start: '10:15', end: '12:15' })
  })

  it('keeps a start that is already on the grid', () => {
    expect(nextSlot(new Date('2026-12-01T09:00:00Z'))).toEqual({ date: '2026-12-01', start: '10:00', end: '12:00' })
  })

  it('gives no slot when two hours would run past midnight', () => {
    expect(nextSlot(new Date('2026-12-01T21:30:00Z'))).toBeNull()
  })
})

describe('isOpenFor', () => {
  const lot = { opensAt: '07:00:00', closesAt: '22:00:00' }

  it('accepts a slot that ends at closing time', () => {
    expect(isOpenFor(lot, { date: '2026-12-01', start: '20:00', end: '22:00' })).toBe(true)
  })

  it('rejects a slot that starts before opening or ends after closing', () => {
    expect(isOpenFor(lot, { date: '2026-12-01', start: '06:45', end: '08:45' })).toBe(false)
    expect(isOpenFor(lot, { date: '2026-12-01', start: '20:15', end: '22:15' })).toBe(false)
  })
})

describe('formatDuration', () => {
  it('shows hours and minutes, leaving out a zero part', () => {
    expect(formatDuration('10:00', '11:15')).toBe('1 h 15 min')
    expect(formatDuration('10:00', '12:00')).toBe('2 h')
    expect(formatDuration('10:00', '10:45')).toBe('45 min')
  })
})

describe('formatDate', () => {
  it('shows a lot date without moving it to another day', () => {
    expect(formatDate('2026-12-01')).toBe('1 Dec 2026')
  })
})
