import { useState } from 'react'
import { Link } from 'react-router'
import { Pager } from '../../components/Pager'
import { formatRate, formatWindow, useLots } from './api'

export function LotsPage() {
  const [page, setPage] = useState(1)
  const lots = useLots(page)

  return (
    <section>
      <h2>Parking lots</h2>
      {lots.isPending && <p role="status">Loading lots…</p>}
      {lots.isError && <p role="alert">We could not load the parking lots. Please try again.</p>}
      {lots.data && lots.data.items.length === 0 && <p>There are no parking lots yet.</p>}
      {lots.data && lots.data.items.length > 0 && (
        <>
          <table>
            <thead>
              <tr>
                <th scope="col">Lot</th>
                <th scope="col">Address</th>
                <th scope="col">Rate</th>
                <th scope="col">Open</th>
                <th scope="col">Spaces</th>
              </tr>
            </thead>
            <tbody>
              {lots.data.items.map((lot) => (
                <tr key={lot.id}>
                  <td>
                    <Link to={`/lots/${lot.id}`}>{lot.name}</Link>
                  </td>
                  <td>{lot.address}</td>
                  <td>{formatRate(lot)}</td>
                  <td>{formatWindow(lot)}</td>
                  <td>{lot.totalSpaces}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pager page={page} totalPages={Number(lots.data.totalPages ?? 1)} onChange={setPage} />
        </>
      )}
    </section>
  )
}
