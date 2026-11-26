import { describe, expect, it } from 'vitest'
import {
  ALREADY_CANCELLED,
  canCancel,
  cancelErrorMessage,
  formatPrice,
  BookingRuleError,
  groupReservations,
  PLATE_OVERLAP,
  type Reservation,
  RESERVATION_LIMIT,
  RESERVATION_NOT_FOUND,
  SpaceTakenError,
  toBookingErrors,
  toConflictError,
  UNEXPECTED_ERROR,
} from './api'

describe('toConflictError', () => {
  it('sends a taken space back to the space plan', () => {
    expect(toConflictError({ code: 'space_taken' })).toBeInstanceOf(SpaceTakenError)
  })

  it('treats a 409 without a code as a taken space', () => {
    expect(toConflictError(undefined)).toBeInstanceOf(SpaceTakenError)
  })

  it('puts a plate overlap next to the plate field', () => {
    const error = toConflictError({ code: 'plate_overlap' })

    expect(error).toBeInstanceOf(BookingRuleError)
    expect((error as BookingRuleError).errors).toEqual({ fields: { plate: PLATE_OVERLAP } })
  })

  it('puts the reservation limit above the form', () => {
    const error = toConflictError({ code: 'reservation_limit' })

    expect(error).toBeInstanceOf(BookingRuleError)
    expect((error as BookingRuleError).errors).toEqual({ fields: {}, form: RESERVATION_LIMIT })
  })
})

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

describe('groupReservations', () => {
  const at = (id: string, state: Reservation['state'], start: string): Reservation => ({
    id,
    lotName: 'Centar',
    spaceLabel: id,
    start,
    end: start,
    plate: 'A12K345',
    price: 5,
    currency: 'BAM',
    state,
  })

  it('puts the active ones in upcoming, soonest first, and the rest in past, newest first', () => {
    const groups = groupReservations([
      at('later', 'Upcoming', '2026-12-03T09:00:00Z'),
      at('now', 'InProgress', '2026-12-01T09:00:00Z'),
      at('cancelled', 'Cancelled', '2026-12-05T09:00:00Z'),
      at('old', 'Finished', '2026-11-01T09:00:00Z'),
    ])

    expect(groups.upcoming.map((r) => r.id)).toEqual(['now', 'later'])
    expect(groups.past.map((r) => r.id)).toEqual(['cancelled', 'old'])
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
