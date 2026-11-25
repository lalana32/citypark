import type { ReactNode } from 'react'
import { formatPrice } from '../reservations/api'
import type { Quote } from './api'
import { formatDate, formatDuration, type Interval } from './interval'

export const QUOTE_FAILED = 'We could not load the price. Please try again.'

type Props = {
  /** The interval the free spaces were checked for; null before the first check. */
  checked: Interval | null
  spaceLabel: string | null
  quote: { data?: Quote; isError: boolean }
  children?: ReactNode
}

export function SummaryPanel({ checked, spaceLabel, quote, children }: Props) {
  return (
    <aside className="site-card lot-summary" aria-labelledby="lot-summary-title">
      <h2 id="lot-summary-title" className="lot-summary-title">
        Your parking
      </h2>

      {checked ? (
        <dl className="lot-summary-list">
          <Row term="Space">{spaceLabel ?? 'Not chosen yet'}</Row>
          <Row term="Date">{formatDate(checked.date)}</Row>
          <Row term="Arrival">{checked.start}</Row>
          <Row term="Departure">{checked.end}</Row>
          <Row term="Duration">{formatDuration(checked.start, checked.end)}</Row>
          <Row term="Price">
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
          </Row>
        </dl>
      ) : (
        <p className="lot-summary-hint">Pick a time and check availability, then choose a free space.</p>
      )}

      {children}
    </aside>
  )
}

function Row({ term, children }: { term: string; children: ReactNode }) {
  return (
    <div className="lot-summary-row">
      <dt>{term}</dt>
      <dd>{children}</dd>
    </div>
  )
}
