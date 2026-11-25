import { useState } from 'react'
import { useLots } from '../lots/api'
import { nextSlot } from '../lots/interval'
import { LotCard } from '../lots/LotCard'
import './locations.css'

export function LocationsSection() {
  const lots = useLots(1)
  const [slot] = useState(() => nextSlot())

  return (
    <section id="locations" className="site-sec site-locations" aria-labelledby="locations-title">
      <div className="site-wrap">
        <p className="site-eyebrow">Parking lots</p>
        <h2 id="locations-title" className="site-h2 site-display">
          Our locations
        </h2>
        <p className="site-sub">
          Pick a parking lot and see how many spaces are free for the next two hours. No account needed to browse.
        </p>

        {lots.isPending && <p role="status">Loading parking lots…</p>}
        {lots.isError && <p role="alert">We could not load the parking lots. Please try again.</p>}
        {lots.data && lots.data.items.length === 0 && <p>There are no parking lots yet.</p>}
        {lots.data && lots.data.items.length > 0 && (
          <ul className="loc-grid">
            {lots.data.items.map((lot, index) => (
              <LotCard key={lot.id} lot={lot} slot={slot} fallbackShift={index % 3} />
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
