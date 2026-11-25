import { DEMO_LOTS, type DemoLot, formatKm, freeSpaces, totalSpaces } from './demoLots'
import './locations.css'

type Props = { onViewLot: (lotId: string) => void }

export function LocationsSection({ onViewLot }: Props) {
  return (
    <section id="locations" className="site-sec site-locations" aria-labelledby="locations-title">
      <div className="site-wrap">
        <p className="site-eyebrow">Parking lots</p>
        <h2 id="locations-title" className="site-h2 site-display">
          Our three locations
        </h2>
        <p className="site-sub">
          Pick a parking lot and see how many spaces are free right now. No account needed to browse.
        </p>

        <ul className="loc-grid">
          {DEMO_LOTS.map((lot, index) => (
            <LocationCard key={lot.id} lot={lot} photoShift={index} onView={() => onViewLot(lot.id)} />
          ))}
        </ul>
      </div>
    </section>
  )
}

function LocationCard({ lot, photoShift, onView }: { lot: DemoLot; photoShift: number; onView: () => void }) {
  const free = freeSpaces(lot)

  return (
    <li className="site-card loc-card">
      <div className="loc-photo">
        <LotPhoto shift={photoShift} />
        <span className="loc-pill">
          <i aria-hidden="true" />
          {free} free spaces
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
            <b className="site-display">{formatKm(lot.pricePerHour)}</b>
            <span>per hour</span>
          </p>
        </div>

        <dl className="loc-tiles">
          <div className="loc-tile">
            <dt>Total spaces</dt>
            <dd className="site-display">{totalSpaces()}</dd>
          </div>
          <div className="loc-tile">
            <dt>Free</dt>
            <dd className="site-display">{free}</dd>
          </div>
        </dl>

        <a
          className="site-btn site-btn-primary loc-view"
          href="#space-selection"
          aria-label={`View parking: ${lot.name}`}
          onClick={onView}
        >
          View parking
        </a>
      </div>
    </li>
  )
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
