import { useRef, useState } from 'react'
import { useSession } from '../auth/api'
import { AuthPrompt } from './AuthPrompt'
import { type DemoLot, formatKm } from './demoLots'
import { ParkingPlan, spaceLabel } from './ParkingPlan'
import { billedHours, formatSlotTime, type Slot } from './slot'
import './spaceSelection.css'

type Props = { lot: DemoLot; slot: Slot }

/** Space plan plus the summary panel. Remount it with a new key to clear the selection when the lot changes. */
export function SpaceSelection({ lot, slot }: Props) {
  const session = useSession()
  const [selected, setSelected] = useState<string | null>(null)
  const [promptOpen, setPromptOpen] = useState(false)
  const [continuing, setContinuing] = useState(false)
  const reserveButton = useRef<HTMLButtonElement>(null)

  const hours = billedHours(slot)
  const label = selected ? spaceLabel(lot, selected) : null

  const select = (space: string) => {
    setSelected(space)
    setContinuing(false)
  }

  const reserve = () => {
    if (session.data) {
      setContinuing(true)
    } else {
      setPromptOpen(true)
    }
  }

  const closePrompt = () => {
    setPromptOpen(false)
    reserveButton.current?.focus()
  }

  return (
    <section id="space-selection" className="site-sec" aria-labelledby="space-selection-title">
      <div className="site-wrap">
        <p className="site-eyebrow">Space selection</p>
        <h2 id="space-selection-title" className="site-h2 site-display">
          Pick the exact space on the plan
        </h2>
        <p className="site-sub">Free spaces are white, occupied ones are dark blue. Click a free space to select it.</p>

        <div className="select-grid">
          <div className="site-card select-plan">
            <div className="select-scroll">
              <ParkingPlan lot={lot} selected={selected} onSelect={select} />
            </div>
            <p className="select-hint">Drag the plan left or right to see all spaces.</p>
          </div>

          <aside className="site-card select-panel" aria-label="Selected space">
            <p className="site-eyebrow select-panel-eyebrow">Selected space</p>
            <p className="site-display select-panel-space" aria-live="polite">
              {label ?? '—'}
            </p>

            <dl className="select-rows">
              <Row term="Parking" value={`${lot.name} · Level ${lot.level}`} />
              <Row term="Arrival" value={formatSlotTime(slot.arrival)} />
              <Row term="Departure" value={formatSlotTime(slot.departure)} />
              <Row term="Duration" value={`${hours} h`} />
              <Row term="Estimated price" value={formatKm(hours * lot.pricePerHour)} />
            </dl>

            <button
              ref={reserveButton}
              type="button"
              className="site-btn site-btn-primary select-reserve"
              disabled={!label}
              onClick={reserve}
            >
              {label ? `Reserve space ${label}` : 'Reserve space'}
            </button>

            {!label && <p className="select-note">Pick a free space on the plan first.</p>}
            {label && !session.data && <p className="select-note">You only need to log in when you confirm.</p>}
            {continuing && (
              <p className="select-note select-note-strong" role="status">
                Continuing to the reservation confirmation.
              </p>
            )}
          </aside>
        </div>
      </div>

      {promptOpen && <AuthPrompt onClose={closePrompt} />}
    </section>
  )
}

function Row({ term, value }: { term: string; value: string }) {
  return (
    <div className="select-row">
      <dt>{term}</dt>
      <dd>{value}</dd>
    </div>
  )
}
