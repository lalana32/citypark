import { Link } from 'react-router'
import { AnimatedCityMap } from './cityMap/AnimatedCityMap'
import { FaqSection } from './FaqSection'
import { HowItWorksSection } from './HowItWorksSection'
import { LocationsSection } from './LocationsSection'
import { PlanPreviewSection } from './PlanPreviewSection'
import './home.css'

/** Public landing page: the hero with the animated city map, then the browsing sections. */
export function HomePage() {
  return (
    <>
      <Hero />
      <LocationsSection />
      <HowItWorksSection />
      <PlanPreviewSection />
      <FaqSection />
    </>
  )
}

function Hero() {
  return (
    <section className="hero" aria-labelledby="hero-title">
      <AnimatedCityMap className="hero-map" />
      <div className="hero-scrim" aria-hidden="true" />

      <div className="hero-content">
        <h1 id="hero-title" className="hero-title">
          <span>Stop circling.</span> <span>Start parking.</span>
        </h1>
        <p className="hero-text">
          Check availability across the city's parking lots and reserve a space for exactly the time you need.
        </p>
        <Link className="hero-cta" to="/lots">
          Find parking
        </Link>
      </div>
    </section>
  )
}

