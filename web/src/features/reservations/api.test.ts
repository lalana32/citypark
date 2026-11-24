import { describe, expect, it } from 'vitest'
import { formatPrice, toBookingErrors, UNEXPECTED_ERROR } from './api'

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
