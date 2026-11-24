import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import type { components } from '../../api/schema'
import { sessionKey } from '../auth/api'
import { lotsKey } from '../lots/api'

export type Reservation = components['schemas']['ReservationResponse']
export type ReservationPage = components['schemas']['PagedResultOfReservationResponse']
export type Booking = { spaceId: string; start: string; end: string; plate: string }
export type BookingField = 'start' | 'end' | 'plate'
export type BookingErrors = { fields: Partial<Record<BookingField, string>>; form?: string }

type Problem = components['schemas']['HttpValidationProblemDetails']

const BOOKING_FIELDS: readonly string[] = ['start', 'end', 'plate']
const PAGE_SIZE = 10

export const reservationsKey = ['reservations'] as const

// The server allows a cancel until the start (R9); the screen stops offering it a bit earlier.
export const CANCEL_CUTOFF_MINUTES = 15

export function useMyReservations(page: number) {
  return useQuery({
    queryKey: [...reservationsKey, 'mine', page],
    queryFn: async (): Promise<ReservationPage> => {
      const { data, response } = await api.GET('/api/v1/reservations', {
        params: { query: { page, pageSize: PAGE_SIZE } },
      })
      if (!data) {
        throw new Error(`The reservations request failed with ${response.status}.`)
      }
      return data
    },
    placeholderData: keepPreviousData,
  })
}

export function canCancel(reservation: Pick<Reservation, 'state' | 'start'>, now: number): boolean {
  const remaining = new Date(reservation.start).getTime() - now
  return reservation.state === 'Upcoming' && remaining >= CANCEL_CUTOFF_MINUTES * 60_000
}

export const SPACE_TAKEN = 'Someone has just booked this space. The free spaces are refreshed, so please pick another one.'
export const UNEXPECTED_ERROR = 'Something went wrong. Please try again.'

/** 409: another booking won the race for the space. */
export class SpaceTakenError extends Error {
  constructor() {
    super(SPACE_TAKEN)
    this.name = 'SpaceTakenError'
  }
}

/** 401: the session is gone, so the user has to sign in again. */
export class SignedOutError extends Error {
  constructor() {
    super('The session has ended.')
    this.name = 'SignedOutError'
  }
}

/** 400 or 422: the request broke a shape or business rule. */
export class BookingRuleError extends Error {
  readonly errors: BookingErrors

  constructor(errors: BookingErrors) {
    super(errors.form ?? 'The booking is invalid.')
    this.name = 'BookingRuleError'
    this.errors = errors
  }
}

export function useCreateReservation(lotId: string) {
  const queryClient = useQueryClient()
  const refreshAvailability = () => queryClient.invalidateQueries({ queryKey: [...lotsKey, lotId, 'availability'] })

  return useMutation({
    mutationFn: async (body: Booking): Promise<Reservation> => {
      const { data, error, response } = await api.POST('/api/v1/reservations', { body })
      if (data) {
        return data
      }
      switch (response.status) {
        case 401:
          throw new SignedOutError()
        case 409:
          throw new SpaceTakenError()
        case 400:
        case 422:
          throw new BookingRuleError(toBookingErrors(error as Problem | undefined))
        default:
          throw new Error(`The reservation request failed with ${response.status}.`)
      }
    },
    // Either way the free spaces have changed: we took one, or someone else did.
    onSuccess: refreshAvailability,
    onError: async (error) => {
      if (error instanceof SpaceTakenError) {
        await refreshAvailability()
      } else if (error instanceof SignedOutError) {
        queryClient.setQueryData(sessionKey, null)
      }
    },
  })
}

export function toBookingErrors(problem: Problem | undefined): BookingErrors {
  const result: BookingErrors = { fields: {} }
  const unmatched: string[] = []

  for (const [key, messages] of Object.entries(problem?.errors ?? {})) {
    if (BOOKING_FIELDS.includes(key)) {
      result.fields[key as BookingField] = messages[0]
    } else {
      unmatched.push(...messages)
    }
  }

  if (unmatched.length > 0) {
    result.form = unmatched.join(' ')
  } else if (Object.keys(result.fields).length === 0) {
    result.form = problem?.detail ?? UNEXPECTED_ERROR
  }

  return result
}

export function formatPrice(reservation: Pick<Reservation, 'price' | 'currency'>): string {
  return `${Number(reservation.price).toFixed(2)} ${reservation.currency}`
}
