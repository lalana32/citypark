import { type ReactNode, useId } from 'react'
import { formatLotDay, formatLotTimes } from '../lots/interval'
import { formatPrice, type Reservation } from './api'
import { StateBadge } from './ReservationParts'

type Props = {
  reservation: Reservation
  /** The cancel control, or nothing when the reservation cannot be cancelled. */
  actions?: ReactNode
}

export function ReservationCard({ reservation, actions }: Props) {
  const titleId = useId()

  return (
    <article className="site-card res-card" aria-labelledby={titleId}>
      <header className="res-card-head">
        <h3 id={titleId} className="site-display res-card-title">
          {reservation.lotName}
        </h3>
        <StateBadge state={reservation.state} />
      </header>

      <dl className="res-facts">
        <Fact term="Space">{reservation.spaceLabel}</Fact>
        <Fact term="License plate">{reservation.plate}</Fact>
        <Fact term="Date">{formatLotDay(reservation.start)}</Fact>
        <Fact term="Time">{formatLotTimes(reservation.start, reservation.end)}</Fact>
        <Fact term="Price">{formatPrice(reservation)}</Fact>
      </dl>

      {actions && <div className="res-card-foot">{actions}</div>}
    </article>
  )
}

function Fact({ term, children }: { term: string; children: ReactNode }) {
  return (
    <div>
      <dt>{term}</dt>
      <dd>{children}</dd>
    </div>
  )
}
