import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '../../api/client'
import type { components } from '../../api/schema'

export type Lot = components['schemas']['LotResponse']
export type LotPage = components['schemas']['PagedResultOfLotResponse']

export const lotsKey = ['lots'] as const

const PAGE_SIZE = 20

export function useLots(page: number) {
  return useQuery({
    queryKey: [...lotsKey, 'list', page],
    queryFn: async (): Promise<LotPage> => {
      const { data, response } = await api.GET('/api/v1/lots', { params: { query: { page, pageSize: PAGE_SIZE } } })
      if (!data) {
        throw new Error(`The lots request failed with ${response.status}.`)
      }
      return data
    },
    placeholderData: keepPreviousData,
  })
}

export function formatRate(lot: Pick<Lot, 'hourlyPrice' | 'currency'>): string {
  return `${Number(lot.hourlyPrice).toFixed(2)} ${lot.currency} / hour`
}

// "07:00:00" -> "07:00"
export function formatWindow(lot: Pick<Lot, 'opensAt' | 'closesAt'>): string {
  return `${lot.opensAt.slice(0, 5)}–${lot.closesAt.slice(0, 5)}`
}

export type Availability = components['schemas']['AvailabilityResponse']
export type AvailabilityErrors = { fields: Partial<Record<'start' | 'end', string>>; form?: string }

type Problem = components['schemas']['HttpValidationProblemDetails']

export const LOT_NOT_FOUND = 'This parking lot does not exist.'
export const UNEXPECTED_ERROR = 'Something went wrong. Please try again.'

export class LotNotFoundError extends Error {
  constructor() {
    super(LOT_NOT_FOUND)
    this.name = 'LotNotFoundError'
  }
}

export class AvailabilityError extends Error {
  readonly errors: AvailabilityErrors

  constructor(errors: AvailabilityErrors) {
    super(errors.form ?? 'The interval is invalid.')
    this.name = 'AvailabilityError'
    this.errors = errors
  }
}

export function useLot(id: string) {
  return useQuery({
    queryKey: [...lotsKey, id],
    queryFn: async (): Promise<Lot> => {
      const { data, response } = await api.GET('/api/v1/lots/{id}', { params: { path: { id } } })
      if (response.status === 404) {
        throw new LotNotFoundError()
      }
      if (!data) {
        throw new Error(`The lot request failed with ${response.status}.`)
      }
      return data
    },
    retry: (failures, error) => !(error instanceof LotNotFoundError) && failures < 3,
  })
}

/** Runs only once an interval is given; start and end are ISO instants with an offset. */
export function useAvailability(id: string, interval: { start: string; end: string } | null) {
  return useQuery({
    queryKey: [...lotsKey, id, 'availability', interval?.start, interval?.end],
    queryFn: async (): Promise<Availability> => {
      const query = interval!
      const { data, error, response } = await api.GET('/api/v1/lots/{id}/availability', {
        params: { path: { id }, query },
      })
      if (!data) {
        throw response.status === 400 || response.status === 422
          ? new AvailabilityError(toAvailabilityErrors(error as Problem | undefined))
          : new Error(`The availability request failed with ${response.status}.`)
      }
      return data
    },
    enabled: interval !== null,
    // A rule violation will not pass on a second try.
    retry: (failures, error) => !(error instanceof AvailabilityError) && failures < 3,
  })
}

export function toAvailabilityErrors(problem: Problem | undefined): AvailabilityErrors {
  const result: AvailabilityErrors = { fields: {} }
  const unmatched: string[] = []

  for (const [key, messages] of Object.entries(problem?.errors ?? {})) {
    if (key === 'start' || key === 'end') {
      result.fields[key] = messages[0]
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

export type Quote = components['schemas']['QuoteResponse']

/** The price for an interval the availability check has already accepted. */
export function useQuote(id: string, interval: { start: string; end: string } | null) {
  return useQuery({
    queryKey: [...lotsKey, id, 'quote', interval?.start, interval?.end],
    queryFn: async (): Promise<Quote> => {
      const { data, response } = await api.GET('/api/v1/lots/{id}/quote', {
        params: { path: { id }, query: interval! },
      })
      if (!data) {
        throw new Error(`The quote request failed with ${response.status}.`)
      }
      return data
    },
    enabled: interval !== null,
  })
}
