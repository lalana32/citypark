import { useState } from 'react'
import { Pager } from '../../components/Pager'
import { useDocumentTitle } from '../../components/useDocumentTitle'
import { formatLotDateTime } from '../lots/interval'
import { formatPrice } from '../reservations/api'
import { CancelAction, StateBadge } from '../reservations/ReservationParts'
import { adminCanCancel, type AdminReservation, useAdminCancelReservation, useAdminReservations } from './api'

export function AdminReservationsPage() {
  const [page, setPage] = useState(1)
  const reservations = useAdminReservations(page)
  useDocumentTitle('All reservations')

  return (
    <section>
      <header className="admin-header">
        <h2 className="site-h2 site-display">All reservations</h2>
      </header>
      {reservations.isPending && (
        <p role="status" className="admin-loading">
          Loading reservations…
        </p>
      )}
      {reservations.isError && (
        <p role="alert" className="admin-alert">
          We could not load the reservations. Please try again.
        </p>
      )}
      {reservations.data && reservations.data.items.length === 0 && (
        <p className="admin-empty">There are no reservations yet.</p>
      )}
      {reservations.data && reservations.data.items.length > 0 && (
        <>
          <div className="site-card admin-table-card">
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">Lot</th>
                  <th scope="col">Space</th>
                  <th scope="col">From</th>
                  <th scope="col">To</th>
                  <th scope="col">Plate</th>
                  <th scope="col" className="admin-num">
                    Price
                  </th>
                  <th scope="col">State</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {reservations.data.items.map((reservation) => (
                  <tr key={reservation.id}>
                    <td className="admin-strong">{reservation.lotName}</td>
                    <td>{reservation.spaceLabel}</td>
                    <td className="admin-nowrap">{formatLotDateTime(reservation.start)}</td>
                    <td className="admin-nowrap">{formatLotDateTime(reservation.end)}</td>
                    <td className="admin-nowrap">{reservation.plate}</td>
                    <td className="admin-num admin-nowrap">{formatPrice(reservation)}</td>
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
          </div>
          <p className="admin-footnote">Times are local time in Sarajevo.</p>
          <div className="admin-pager">
            <Pager page={page} totalPages={Number(reservations.data.totalPages ?? 1)} onChange={setPage} />
          </div>
        </>
      )}
    </section>
  )
}

function AdminCancel({ reservation }: { reservation: AdminReservation }) {
  const cancel = useAdminCancelReservation()
  return <CancelAction reservationId={reservation.id} cancellable={adminCanCancel(reservation)} cancel={cancel} />
}
