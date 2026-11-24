import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { Pager } from '../../components/Pager'
import { formatLotDateTime } from '../lots/interval'
import {
  canCancel,
  CancelRejectedError,
  formatPrice,
  type Reservation,
  UNEXPECTED_ERROR,
  useCancelReservation,
  useMyReservations,
} from './api'

const STATE_LABELS: Record<Reservation['state'], string> = {
  Upcoming: 'Upcoming',
  InProgress: 'In progress',
  Finished: 'Finished',
  Cancelled: 'Cancelled',
}

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
                    <span className={`badge badge-${reservation.state}`}>{STATE_LABELS[reservation.state]}</span>
                  </td>
                  <td>
                    <CancelAction reservation={reservation} cancellable={canCancel(reservation, now)} />
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

function CancelAction({ reservation, cancellable }: { reservation: Reservation; cancellable: boolean }) {
  const cancel = useCancelReservation()
  const [confirming, setConfirming] = useState(false)

  const error =
    cancel.error instanceof CancelRejectedError ? cancel.error.message : cancel.isError ? UNEXPECTED_ERROR : null

  const confirm = () => cancel.mutate(reservation.id, { onSettled: () => setConfirming(false) })

  return (
    <>
      {cancellable && !confirming && (
        <button type="button" onClick={() => setConfirming(true)}>
          Cancel
        </button>
      )}
      {cancellable && confirming && (
        <div role="group" aria-label="Confirm cancellation">
          <span>Cancel this reservation?</span>
          <button type="button" disabled={cancel.isPending} onClick={confirm}>
            {cancel.isPending ? 'Cancelling…' : 'Yes, cancel'}
          </button>
          <button type="button" disabled={cancel.isPending} onClick={() => setConfirming(false)}>
            Keep it
          </button>
        </div>
      )}
      {error && <p role="alert">{error}</p>}
    </>
  )
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
