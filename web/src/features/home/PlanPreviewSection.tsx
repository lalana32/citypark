import { Link } from 'react-router'
import { ParkingPlan } from '../lots/plan/ParkingPlan'
import type { PlanSpace } from '../lots/plan/planLayout'
import './planPreview.css'

// A fixed example, not live data: it only shows what picking a space looks like.
const OCCUPIED = ['A-02', 'A-05', 'A-07', 'A-10', 'A-11']
const SELECTED = 'A-04'

const EXAMPLE_SPACES: PlanSpace[] = Array.from({ length: 12 }, (_, index) => {
  const label = `A-${String(index + 1).padStart(2, '0')}`
  const state = label === SELECTED ? 'selected' : OCCUPIED.includes(label) ? 'occupied' : 'free'
  return { id: label, label, state }
})

const EXAMPLE_DETAILS = [
  ['Parking', 'Parking 01 · Level A'],
  ['Arrival', 'Today · 10:00'],
  ['Departure', 'Today · 12:00'],
  ['Duration', '2 h'],
  ['Estimated price', '5.00 BAM'],
] as const

export function PlanPreviewSection() {
  return (
    <section id="space-plan" className="site-sec" aria-labelledby="space-plan-title">
      <div className="site-wrap">
        <p className="site-eyebrow">Space selection</p>
        <h2 id="space-plan-title" className="site-h2 site-display">
          Pick the exact space on the plan
        </h2>
        <p className="site-sub">
          Free spaces are white, occupied ones show a car. On each lot's page you pick the space you want.
        </p>

        <div className="preview-grid">
          <div className="site-card preview-plan">
            <div className="plan-scroll">
              <ParkingPlan title="Parking 01 · Level A" spaces={EXAMPLE_SPACES} />
            </div>
            <p className="plan-hint">Scroll the plan sideways to see all spaces.</p>
          </div>

          <aside className="site-card preview-panel" aria-labelledby="preview-panel-title">
            <p id="preview-panel-title" className="site-eyebrow preview-eyebrow">
              Selected space
            </p>
            <p className="site-display preview-space">{SELECTED}</p>

            <dl className="preview-rows">
              {EXAMPLE_DETAILS.map(([term, value]) => (
                <div key={term} className="preview-row">
                  <dt>{term}</dt>
                  <dd>{value}</dd>
                </div>
              ))}
            </dl>

            <Link className="site-btn site-btn-primary preview-cta" to="/lots">
              Find a parking space
            </Link>
            <p className="preview-note">This is an example. Live free spaces are on each lot's page.</p>
          </aside>
        </div>
      </div>
    </section>
  )
}
