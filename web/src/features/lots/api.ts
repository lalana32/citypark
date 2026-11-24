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
