import type { ReactNode } from 'react'
import { formatPrice } from '../reservations/api'
import type { Quote } from './api'
import { formatDate, formatDuration, type Interval } from './interval'

export const QUOTE_FAILED = 'We could not load the price. Please try again.'

type Props = {
  lotName: string
  /** The interval the free spaces were checked for; null before the first check. */
  checked: Interval | null
  spaceLabel: string | null
  quote: { data?: Quote; isError: boolean }
  children?: ReactNode
}

export function SummaryPanel({ lotName, checked, spaceLabel, quote, children }: Props) {
  return (
    <aside className="site-card lot-summary" aria-labelledby="lot-summary-title">
      <h2 id="lot-summary-title" className="lot-summary-title">
        Your parking
      </h2>

      {checked ? (
        <dl className="lot-summary-list">
          <SummaryRow term="Lot">{lotName}</SummaryRow>
          <SummaryRow term="Space">{spaceLabel ?? 'Not chosen yet'}</SummaryRow>
          <SummaryRow term="Date">{formatDate(checked.date)}</SummaryRow>
          <SummaryRow term="Arrival">{checked.start}</SummaryRow>
          <SummaryRow term="Departure">{checked.end}</SummaryRow>
          <SummaryRow term="Duration">{formatDuration(checked.start, checked.end)}</SummaryRow>
          <SummaryRow term="Price">
            {quote.data ? (
              <>
                <strong className="lot-summary-price">{formatPrice(quote.data)}</strong>{' '}
                <span className="lot-summary-note">({quote.data.billedHours} h billed)</span>
              </>
            ) : quote.isError ? (
              <span className="lot-field-error">{QUOTE_FAILED}</span>
            ) : (
              'Calculating…'
            )}
          </SummaryRow>
        </dl>
      ) : (
        <p className="lot-summary-hint">Pick a time and check availability, then choose a free space.</p>
      )}

      {children}
    </aside>
  )
}

export function SummaryRow({ term, children }: { term: string; children: ReactNode }) {
  return (
    <div className="lot-summary-row">
      <dt>{term}</dt>
      <dd>{children}</dd>
    </div>
  )
}
