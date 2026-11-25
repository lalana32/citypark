import { useState } from 'react'
import { Link } from 'react-router'
import { type Lot, useAvailability, useLots } from '../lots/api'
import { type Interval, isOpenFor, nextSlot, toLotInstant } from '../lots/interval'
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
              <LocationCard key={lot.id} lot={lot} slot={slot} photoShift={index % 3} />
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

function LocationCard({ lot, slot, photoShift }: { lot: Lot; slot: Interval | null; photoShift: number }) {
  const open = slot !== null && isOpenFor(lot, slot)
  const availability = useAvailability(
    lot.id,
    open ? { start: toLotInstant(slot.date, slot.start), end: toLotInstant(slot.date, slot.end) } : null,
  )
  const free = freeLabel(open, availability)

  return (
    <li className="site-card loc-card">
      <div className="loc-photo">
        <LotPhoto shift={photoShift} />
        <span className={open ? 'loc-pill' : 'loc-pill loc-pill-closed'}>
          <i aria-hidden="true" />
          {free.pill}
        </span>
      </div>

      <div className="loc-body">
        <div className="loc-top">
          <div>
            <h3 className="loc-name site-display">{lot.name}</h3>
            <p className="loc-address">
              <PinIcon />
              {lot.address}
            </p>
          </div>
          <p className="loc-price">
            <b className="site-display">{formatKm(lot.hourlyPrice)}</b>
            <span>per hour</span>
          </p>
        </div>

        <dl className="loc-tiles">
          <div className="loc-tile">
            <dt>Total spaces</dt>
            <dd className="site-display">{lot.totalSpaces}</dd>
          </div>
          <div className="loc-tile">
            <dt>Free</dt>
            <dd className="site-display">{free.tile}</dd>
          </div>
        </dl>

        <Link className="site-btn site-btn-primary loc-view" to={`/lots/${lot.id}`} aria-label={`View parking: ${lot.name}`}>
          View parking
        </Link>
      </div>
    </li>
  )
}

function freeLabel(open: boolean, availability: ReturnType<typeof useAvailability>) {
  if (!open) {
    return { pill: 'Closed at this time', tile: 'Closed' }
  }
  if (availability.data) {
    const count = String(availability.data.availableSpaces)
    return { pill: `${count} free spaces`, tile: count }
  }
  if (availability.isError) {
    return { pill: 'Free spaces unavailable', tile: '—' }
  }
  return { pill: 'Checking free spaces…', tile: '…' }
}

// The API sends BAM; the site shows the local "KM".
function formatKm(amount: Lot['hourlyPrice']) {
  return `${Number(amount).toFixed(2)} KM`
}

/** Placeholder illustration until real photos of the lots are cleared for publishing. */
function LotPhoto({ shift }: { shift: number }) {
  const offset = shift * -40

  return (
    <svg viewBox="0 0 400 300" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
      <rect width="400" height="300" fill="#1a2236" />
      <path d="M0 0H400V70H0Z" fill="#232d45" />
      <g stroke="#3a4766" strokeWidth="6">
        <path d="M30 80H370M30 104H370" />
      </g>
      <path d="M0 300L150 150H250L400 300Z" fill="#2b354f" />
      <path d="M200 160V300" stroke="#e8ff3a" strokeWidth="4" strokeDasharray="14 12" />
      <g transform={`translate(${offset} 0)`} fill="#3e63d8" opacity="0.9">
        <rect x="40" y="150" width="56" height="90" rx="10" />
        <rect x="330" y="160" width="50" height="80" rx="10" fill="#8db1ff" />
      </g>
      <g fill="#f2f5ff" opacity="0.85">
        <rect x="60" y="40" width="80" height="6" rx="3" />
        <rect x="260" y="40" width="80" height="6" rx="3" />
      </g>
    </svg>
  )
}

function PinIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <path d="M12 21s7-6.2 7-11.5A7 7 0 0 0 5 9.5C5 14.8 12 21 12 21z" strokeLinejoin="round" />
      <circle cx="12" cy="9.5" r="2.5" />
    </svg>
  )
}
