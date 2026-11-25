import { Link } from 'react-router'
import { formatLotDateTime } from '../lots/interval'
import { SummaryRow } from '../lots/SummaryPanel'
import { formatPrice, type Reservation } from './api'

export function BookingSuccess({ reservation }: { reservation: Reservation }) {
  return (
    <section className="lot-booking lot-success" aria-labelledby="booking-success-title">
      <h3 id="booking-success-title" className="lot-booking-title lot-success-title">
        Booking confirmed
      </h3>
      <p className="lot-success-lead">
        Your space is reserved. You can see or cancel it under My reservations.
      </p>

      <dl className="lot-summary-list">
        <SummaryRow term="Lot">{reservation.lotName}</SummaryRow>
        <SummaryRow term="Space">{reservation.spaceLabel}</SummaryRow>
        <SummaryRow term="License plate">{reservation.plate}</SummaryRow>
        <SummaryRow term="From">{formatLotDateTime(reservation.start)}</SummaryRow>
        <SummaryRow term="To">{formatLotDateTime(reservation.end)}</SummaryRow>
        <SummaryRow term="Price">
          <strong className="lot-summary-price">{formatPrice(reservation)}</strong>
        </SummaryRow>
      </dl>
      <p className="lot-field-hint">Times are local time in Sarajevo.</p>

      <div className="lot-booking-actions">
        <Link className="site-btn site-btn-primary" to="/reservations">
          My reservations
        </Link>
        <Link className="site-btn site-btn-secondary" to="/">
          Back to home
        </Link>
      </div>
    </section>
  )
}

