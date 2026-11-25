import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { Pager } from '../../components/Pager'
import { canCancel, type Reservation, useCancelReservation, useMyReservations } from './api'
import { ReservationCard } from './ReservationCard'
import { CancelAction } from './ReservationParts'
import './reservations.css'

export function MyReservationsPage() {
  const [page, setPage] = useState(1)
  const reservations = useMyReservations(page)
  const now = useNow()

  return (
    <section className="res-page">
      <div className="site-wrap">
        <header className="res-header">
          <p className="site-eyebrow">Your bookings</p>
          <h1 className="site-h2 site-display">My reservations</h1>
          <p className="site-sub">Times are local time in Sarajevo.</p>
        </header>

        {reservations.isPending && <p role="status">Loading reservations…</p>}
        {reservations.isError && <p role="alert">We could not load your reservations. Please try again.</p>}
        {reservations.data && reservations.data.items.length === 0 && (
          <p>
            You have no reservations yet. <Link to="/lots">Find a parking lot</Link>
          </p>
        )}
        {reservations.data && reservations.data.items.length > 0 && (
          <>
            <ul className="res-list">
              {reservations.data.items.map((reservation) => (
                <li key={reservation.id}>
                  <ReservationCard
                    reservation={reservation}
                    actions={<MyCancel reservation={reservation} cancellable={canCancel(reservation, now)} />}
                  />
                </li>
              ))}
            </ul>
            <Pager page={page} totalPages={Number(reservations.data.totalPages ?? 1)} onChange={setPage} />
          </>
        )}
      </div>
    </section>
  )
}

function MyCancel({ reservation, cancellable }: { reservation: Reservation; cancellable: boolean }) {
  const cancel = useCancelReservation()
  return <CancelAction reservationId={reservation.id} cancellable={cancellable} cancel={cancel} />
}

/** The current time, refreshed every 30 seconds so the cancel button disappears at the cutoff. */
function useNow(intervalMs = 30_000): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(timer)
  }, [intervalMs])

  return now
}
