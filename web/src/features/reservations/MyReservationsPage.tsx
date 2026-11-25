import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { Pager } from '../../components/Pager'
import { formatLotDateTime } from '../lots/interval'
import { canCancel, formatPrice, type Reservation, useCancelReservation, useMyReservations } from './api'
import { CancelAction, StateBadge } from './ReservationParts'

export function MyReservationsPage() {
  const [page, setPage] = useState(1)
  const reservations = useMyReservations(page)
  const now = useNow()

  return (
    <section>
      <h2>My reservations</h2>
      {reservations.isPending && <p role="status">Loading reservations…</p>}
      {reservations.isError && <p role="alert">We could not load your reservations. Please try again.</p>}
      {reservations.data && reservations.data.items.length === 0 && (
        <p>
          You have no reservations yet. <Link to="/lots">Find a parking lot</Link>
        </p>
      )}
      {reservations.data && reservations.data.items.length > 0 && (
        <>
          <table>
            <thead>
              <tr>
                <th scope="col">Lot</th>
                <th scope="col">Space</th>
                <th scope="col">From</th>
                <th scope="col">To</th>
                <th scope="col">Plate</th>
                <th scope="col">Price</th>
                <th scope="col">State</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {reservations.data.items.map((reservation) => (
                <tr key={reservation.id}>
                  <td>{reservation.lotName}</td>
                  <td>{reservation.spaceLabel}</td>
                  <td>{formatLotDateTime(reservation.start)}</td>
                  <td>{formatLotDateTime(reservation.end)}</td>
                  <td>{reservation.plate}</td>
                  <td>{formatPrice(reservation)}</td>
                  <td>
                    <StateBadge state={reservation.state} />
                  </td>
                  <td>
                    <MyCancel reservation={reservation} cancellable={canCancel(reservation, now)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>Times are local time in Sarajevo.</p>
          <Pager page={page} totalPages={Number(reservations.data.totalPages ?? 1)} onChange={setPage} />
        </>
      )}
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
