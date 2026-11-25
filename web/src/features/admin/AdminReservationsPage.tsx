import { useState } from 'react'
import { Pager } from '../../components/Pager'
import { formatLotDateTime } from '../lots/interval'
import { formatPrice } from '../reservations/api'
import { CancelAction, StateBadge } from '../reservations/ReservationParts'
import { adminCanCancel, type AdminReservation, useAdminCancelReservation, useAdminReservations } from './api'

export function AdminReservationsPage() {
  const [page, setPage] = useState(1)
  const reservations = useAdminReservations(page)

  return (
    <section>
      <h2>All reservations</h2>
      {reservations.isPending && <p role="status">Loading reservations…</p>}
      {reservations.isError && <p role="alert">We could not load the reservations. Please try again.</p>}
      {reservations.data && reservations.data.items.length === 0 && <p>There are no reservations yet.</p>}
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
                    <AdminCancel reservation={reservation} />
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

function AdminCancel({ reservation }: { reservation: AdminReservation }) {
  const cancel = useAdminCancelReservation()
  return <CancelAction reservationId={reservation.id} cancellable={adminCanCancel(reservation)} cancel={cancel} />
}
