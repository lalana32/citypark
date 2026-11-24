import { describe, expect, it } from 'vitest'
import {
  ALREADY_CANCELLED,
  canCancel,
  cancelErrorMessage,
  formatPrice,
  RESERVATION_NOT_FOUND,
  toBookingErrors,
  UNEXPECTED_ERROR,
} from './api'

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

describe('cancelErrorMessage', () => {
  it('shows the rule the server gives when the reservation has started meanwhile', () => {
    const message = 'A customer can cancel a reservation only before it starts (R9).'

    expect(cancelErrorMessage(422, { detail: message, errors: { general: [message] } })).toBe(message)
  })

  it('says a second cancel is not needed', () => {
    expect(cancelErrorMessage(409, {})).toBe(ALREADY_CANCELLED)
  })

  it('says the reservation is gone', () => {
    expect(cancelErrorMessage(404, {})).toBe(RESERVATION_NOT_FOUND)
  })

  it('falls back to a generic message for anything else', () => {
    expect(cancelErrorMessage(500, undefined)).toBe(UNEXPECTED_ERROR)
  })
})
