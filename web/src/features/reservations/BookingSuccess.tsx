import { formatLotDateTime } from '../lots/interval'
import { formatPrice, type Reservation } from './api'

export function BookingSuccess({ reservation }: { reservation: Reservation }) {
  return (
    <section aria-label="Booking confirmed">
      <h3>Booking confirmed</h3>
      <p>
        Space {reservation.spaceLabel} at {reservation.lotName} is reserved for {reservation.plate}.
      </p>
      <dl>
        <dt>From</dt>
        <dd>{formatLotDateTime(reservation.start)}</dd>
        <dt>To</dt>
        <dd>{formatLotDateTime(reservation.end)}</dd>
        <dt>Price</dt>
        <dd>{formatPrice(reservation)}</dd>
      </dl>
      <p>Times are local time in Sarajevo.</p>
    </section>
  )
}
