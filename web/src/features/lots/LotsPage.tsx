import { useState } from 'react'
import { Pager } from '../../components/Pager'
import { useDocumentTitle } from '../../components/useDocumentTitle'
import { useLots } from './api'
import { nextSlot } from './interval'
import { LotCard } from './LotCard'
import './lotsPage.css'

export function LotsPage() {
  const [page, setPage] = useState(1)
  const [slot] = useState(() => nextSlot())
  const lots = useLots(page)
  useDocumentTitle('Parking lots')

  return (
    <section className="lots-page" aria-labelledby="lots-title">
      <div className="site-wrap">
        <p className="site-eyebrow">Parking lots</p>
        <h1 id="lots-title" className="site-h2 site-display">
          Our locations
        </h1>
        <p className="site-sub">
          Pick a parking lot and see how many spaces are free for the next two hours. No account needed to browse.
        </p>

        {lots.isPending && <p role="status">Loading parking lots…</p>}
        {lots.isError && <p role="alert">We could not load the parking lots. Please try again.</p>}
        {lots.data && lots.data.items.length === 0 && <p>There are no parking lots yet.</p>}
        {lots.data && lots.data.items.length > 0 && (
          <>
            <ul className="loc-grid">
              {lots.data.items.map((lot, index) => (
                <LotCard key={lot.id} lot={lot} slot={slot} fallbackShift={index % 3} />
              ))}
            </ul>
            <div className="lots-pager">
              <Pager page={page} totalPages={Number(lots.data.totalPages ?? 1)} onChange={setPage} />
            </div>
          </>
        )}
      </div>
    </section>
  )
}
