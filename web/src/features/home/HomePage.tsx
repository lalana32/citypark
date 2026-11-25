import { useState } from 'react'
import { Link } from 'react-router'
import { AnimatedCityMap } from './cityMap/AnimatedCityMap'
import { DEMO_LOTS } from './demoLots'
import { LocationsSection } from './LocationsSection'
import { defaultSlot } from './slot'
import { SpaceSelection } from './SpaceSelection'
import './home.css'

/** Public landing page: the hero with the animated city map, then the browsing sections. */
export function HomePage() {
  const [lotId, setLotId] = useState(DEMO_LOTS[0].id)
  const [slot] = useState(() => defaultSlot(new Date()))
  const lot = DEMO_LOTS.find((item) => item.id === lotId) ?? DEMO_LOTS[0]

  return (
    <>
      <Hero />
      <LocationsSection onViewLot={setLotId} />
      <SpaceSelection key={lot.id} lot={lot} slot={slot} />
    </>
  )
}

function Hero() {
  return (
    <section className="hero" aria-labelledby="hero-title">
      <AnimatedCityMap className="hero-map" />
      <div className="hero-scrim" aria-hidden="true" />

      <div className="hero-content">
        <h2 id="hero-title" className="hero-title">
          <span>Stop circling.</span> <span>Start parking.</span>
        </h2>
        <p className="hero-text">
          Check availability across the city's parking lots and reserve a space for exactly the time you need.
        </p>
        <Link className="hero-cta" to="/lots">
          <ParkingIcon />
          Find parking
        </Link>
      </div>
    </section>
  )
}

/** 24 × 16 parking-space glyph: a bay with three stall lines. */
function ParkingIcon() {
  return (
    <svg width="24" height="16" viewBox="0 0 24 16" fill="none" aria-hidden="true">
      <rect x="0.75" y="0.75" width="22.5" height="14.5" rx="2" stroke="currentColor" strokeWidth="1.5" />
      <path d="M6.5 0.75V10M12 0.75V10M17.5 0.75V10" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  )
}
