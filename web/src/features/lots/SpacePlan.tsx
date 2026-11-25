import type { Availability } from './api'
import { ParkingPlan } from './plan/ParkingPlan'
import type { PlanSpace } from './plan/planLayout'

export const NO_FREE_SPACES = 'No free spaces for this time'

type Props = {
  lotName: string
  availability: Availability
  selectedId: string | null
  onSelect: (id: string) => void
}

/** The checked time drawn as the lot plan: occupied spaces show a car, free ones can be picked. */
export function SpacePlan({ lotName, availability, selectedId, onSelect }: Props) {
  const spaces: PlanSpace[] = availability.spaces.map((space) => ({
    id: space.id,
    label: space.label,
    state: !space.isFree ? 'occupied' : space.id === selectedId ? 'selected' : 'free',
  }))

  return (
    <section className="lot-spaces" aria-label="Space plan">
      {availability.availableSpaces === 0 && (
        <div className="lot-empty">
          <p className="lot-empty-title">{NO_FREE_SPACES}</p>
          <p className="lot-empty-hint">Try an earlier or later time, or another date.</p>
        </div>
      )}
      {spaces.length > 0 && (
        <div className="site-card lot-plan">
          <div className="plan-scroll">
            <ParkingPlan title={lotName} spaces={spaces} onSelect={onSelect} />
          </div>
          <p className="plan-hint">Scroll the plan sideways to see all spaces.</p>
        </div>
      )}
    </section>
  )
}
