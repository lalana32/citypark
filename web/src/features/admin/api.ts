import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import type { components } from '../../api/schema'
import { sessionKey } from '../auth/api'
import { lotsKey, type Lot } from '../lots/api'
import { SignedOutError, UNEXPECTED_ERROR } from '../reservations/api'

type Problem = components['schemas']['HttpValidationProblemDetails']

/** What the lot form edits. Times are "HH:mm" as an <input type="time"> gives them. */
export type LotValues = {
  name: string
  address: string
  hourlyPrice: number
  currency: string
  opensAt: string
  closesAt: string
}
export type LotField = keyof LotValues
export type LotErrors = { fields: Partial<Record<LotField, string>>; form?: string }

const LOT_FIELDS: readonly string[] = ['name', 'address', 'hourlyPrice', 'currency', 'opensAt', 'closesAt']

export const NEW_LOT: Partial<LotValues> = { currency: 'BAM', opensAt: '07:00', closesAt: '22:00' }

export const NOT_AN_ADMIN = 'You do not have access to manage parking lots.'

export class LotFormError extends Error {
  readonly errors: LotErrors

  constructor(errors: LotErrors) {
    super(errors.form ?? 'The lot is invalid.')
    this.name = 'LotFormError'
    this.errors = errors
  }
}

export function toLotValues(lot: Lot): LotValues {
  return {
    name: lot.name,
    address: lot.address,
    hourlyPrice: Number(lot.hourlyPrice),
    currency: lot.currency,
    opensAt: lot.opensAt.slice(0, 5),
    closesAt: lot.closesAt.slice(0, 5),
  }
}

// The API reads TimeOnly as "HH:mm:ss".
function toLotRequest(values: LotValues) {
  return { ...values, opensAt: `${values.opensAt}:00`, closesAt: `${values.closesAt}:00` }
}

/** Creates a lot when id is undefined, otherwise replaces that lot. */
export function useSaveLot(id: string | undefined) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (values: LotValues): Promise<Lot> => {
      const body = toLotRequest(values)
      const { data, error, response } = id
        ? await api.PUT('/api/v1/admin/lots/{id}', { params: { path: { id } }, body })
        : await api.POST('/api/v1/admin/lots', { body })
      if (data) {
        return data
      }
      switch (response.status) {
        case 401:
          throw new SignedOutError()
        case 403:
          throw new LotFormError({ fields: {}, form: NOT_AN_ADMIN })
        case 400:
        case 422:
          throw new LotFormError(toLotErrors(error as Problem | undefined))
        default:
          throw new Error(`The lot request failed with ${response.status}.`)
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: lotsKey }),
    onError: (error) => {
      if (error instanceof SignedOutError) {
        queryClient.setQueryData(sessionKey, null)
      }
    },
  })
}

export type Space = components['schemas']['SpaceResponse']

/** 400, 409 or 422 on a new space; the message belongs next to the label field. */
export class SpaceLabelError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'SpaceLabelError'
  }
}

export function useAddSpace(lotId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (label: string): Promise<Space> => {
      const { data, error, response } = await api.POST('/api/v1/admin/lots/{id}/spaces', {
        params: { path: { id: lotId } },
        body: { label },
      })
      if (data) {
        return data
      }
      const problem = error as Problem | undefined
      switch (response.status) {
        case 401:
          throw new SignedOutError()
        case 403:
          throw new SpaceLabelError(NOT_AN_ADMIN)
        case 400:
        case 409:
        case 422:
          throw new SpaceLabelError(problem?.errors?.label?.[0] ?? problem?.detail ?? UNEXPECTED_ERROR)
        default:
          throw new Error(`The space request failed with ${response.status}.`)
      }
    },
    // The lot's space count has changed.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: lotsKey }),
    onError: (error) => {
      if (error instanceof SignedOutError) {
        queryClient.setQueryData(sessionKey, null)
      }
    },
  })
}

export function toLotErrors(problem: Problem | undefined): LotErrors {
  const result: LotErrors = { fields: {} }
  const unmatched: string[] = []

  for (const [key, messages] of Object.entries(problem?.errors ?? {})) {
    if (LOT_FIELDS.includes(key)) {
      result.fields[key as LotField] = messages[0]
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
