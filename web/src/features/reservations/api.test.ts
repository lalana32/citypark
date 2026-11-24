import { describe, expect, it } from 'vitest'
import { canCancel, formatPrice, toBookingErrors, UNEXPECTED_ERROR } from './api'

describe('toBookingErrors', () => {
  it('puts a field rule next to its field', () => {
    const message = 'Plate must not be empty.'

    expect(toBookingErrors({ detail: message, errors: { plate: [message] } })).toEqual({ fields: { plate: message } })
  })

  it('puts a rule without a field above the form', () => {
    const message = "License plate 'X1' does not match the Bosnia and Herzegovina format, for example A12K345."

    expect(toBookingErrors({ detail: message, errors: { general: [message] } })).toEqual({ fields: {}, form: message })
  })

  it('falls back to a generic message when the body has nothing to show', () => {
    expect(toBookingErrors(undefined)).toEqual({ fields: {}, form: UNEXPECTED_ERROR })
  })
})

describe('formatPrice', () => {
  it('always shows two decimals and the currency', () => {
    expect(formatPrice({ price: 5, currency: 'BAM' })).toBe('5.00 BAM')
  })
})

describe('canCancel', () => {
  const start = '2026-12-01T09:00:00+00:00'
  const minutesBefore = (minutes: number) => new Date(start).getTime() - minutes * 60_000

  it('offers the cancel up to 15 minutes before the start', () => {
    expect(canCancel({ state: 'Upcoming', start }, minutesBefore(15))).toBe(true)
  })

  it('hides the cancel when less than 15 minutes remain', () => {
    expect(canCancel({ state: 'Upcoming', start }, minutesBefore(14))).toBe(false)
  })

  it('never offers the cancel for a reservation that is not upcoming', () => {
    for (const state of ['InProgress', 'Finished', 'Cancelled'] as const) {
      expect(canCancel({ state, start }, minutesBefore(60))).toBe(false)
    }
  })
})
