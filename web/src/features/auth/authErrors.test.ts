import { describe, expect, it } from 'vitest'
import { INVALID_CREDENTIALS, UNEXPECTED_ERROR, toAuthFormErrors } from './authErrors'

const FIELDS = ['email', 'password'] as const

function respond(status: number, headers?: Record<string, string>) {
  return new Response(null, { status, headers })
}

describe('toAuthFormErrors', () => {
  it('shows our generic message on 401, not the server detail', () => {
    const errors = toAuthFormErrors(respond(401), { detail: 'User ana@example.com is locked.' }, FIELDS)

    expect(errors).toEqual({ fields: {}, form: INVALID_CREDENTIALS })
  })

  it('tells the user when to retry on 429', () => {
    const errors = toAuthFormErrors(respond(429, { 'Retry-After': '30' }), undefined, FIELDS)

    expect(errors.form).toBe('Too many attempts. Try again in 30 seconds.')
  })

  it('falls back to "later" on 429 without a usable Retry-After', () => {
    expect(toAuthFormErrors(respond(429), undefined, FIELDS).form).toBe('Too many attempts. Try again later.')
    expect(toAuthFormErrors(respond(429, { 'Retry-After': 'soon' }), undefined, FIELDS).form)
      .toBe('Too many attempts. Try again later.')
  })

  it('puts the first message of each known field under that field', () => {
    const problem = { errors: { email: ['Email is required.'], password: ['Too short.', 'Needs a digit.'] } }

    const errors = toAuthFormErrors(respond(422), problem, FIELDS)

    expect(errors).toEqual({ fields: { email: 'Email is required.', password: 'Too short.' } })
  })

  it('shows errors for unknown keys at form level', () => {
    const problem = { errors: { general: ['The account could not be created.'], password: ['Too short.'] } }

    const errors = toAuthFormErrors(respond(422), problem, FIELDS)

    expect(errors).toEqual({ fields: { password: 'Too short.' }, form: 'The account could not be created.' })
  })

  it('uses the detail when there are no field errors', () => {
    const problem = { detail: 'An account with this email already exists.' }

    expect(toAuthFormErrors(respond(409), problem, FIELDS))
      .toEqual({ fields: {}, form: 'An account with this email already exists.' })
  })

  it('falls back to a generic message when the body is not a problem', () => {
    expect(toAuthFormErrors(respond(500), undefined, FIELDS)).toEqual({ fields: {}, form: UNEXPECTED_ERROR })
  })
})
