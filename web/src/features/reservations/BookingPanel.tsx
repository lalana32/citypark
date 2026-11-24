import { type FormEvent, useState } from 'react'
import { formatLotDateTime } from '../lots/interval'
import { BookingRuleError, SignedOutError, SpaceTakenError, UNEXPECTED_ERROR } from './api'

type Props = {
  lotName: string
  spaceLabel: string
  interval: { start: string; end: string }
  pending: boolean
  error: Error | null
  onConfirm: (plate: string) => void
  onDismissError: () => void
  onCancel: () => void
}

/** Two steps: enter the plate, then confirm the summary. */
export function BookingPanel({ lotName, spaceLabel, interval, pending, error, onConfirm, onDismissError, onCancel }: Props) {
  const [plate, setPlate] = useState('')
  const [confirming, setConfirming] = useState(false)

  const errors = error instanceof BookingRuleError ? error.errors : undefined
  // The page handles 409 and 401 itself.
  const unexpected = error !== null && !errors && !(error instanceof SpaceTakenError) && !(error instanceof SignedOutError)
  const alert = unexpected
    ? UNEXPECTED_ERROR
    : [errors?.form, errors?.fields.start, errors?.fields.end].filter(Boolean).join(' ')
  const plateError = errors?.fields.plate

  function edit(next: string) {
    setPlate(next)
    setConfirming(false)
    onDismissError()
  }

  function next(event: FormEvent) {
    event.preventDefault()
    onDismissError()
    setConfirming(true)
  }

  return (
    <section aria-label="Booking">
      <h3>Book space {spaceLabel}</h3>
      {alert && <p role="alert">{alert}</p>}

      {confirming && !errors ? (
        <div>
          <dl>
            <dt>Lot</dt>
            <dd>{lotName}</dd>
            <dt>Space</dt>
            <dd>{spaceLabel}</dd>
            <dt>From</dt>
            <dd>{formatLotDateTime(interval.start)}</dd>
            <dt>To</dt>
            <dd>{formatLotDateTime(interval.end)}</dd>
            <dt>License plate</dt>
            <dd>{plate.trim()}</dd>
          </dl>
          <button type="button" disabled={pending} onClick={() => onConfirm(plate.trim())}>
            Confirm booking
          </button>
          <button type="button" disabled={pending} onClick={() => setConfirming(false)}>
            Change plate
          </button>
          {pending && <p role="status">Booking…</p>}
        </div>
      ) : (
        <form onSubmit={next} noValidate>
          <label htmlFor="booking-plate">License plate</label>
          <input
            id="booking-plate"
            value={plate}
            autoComplete="off"
            onChange={(event) => edit(event.target.value)}
            aria-describedby={plateError ? 'booking-plate-hint booking-plate-error' : 'booking-plate-hint'}
            {...(plateError && { 'aria-invalid': true })}
          />
          <p id="booking-plate-hint">For example A12K345</p>
          {plateError && <p id="booking-plate-error">{plateError}</p>}
          <button type="submit" disabled={plate.trim() === ''}>
            Continue
          </button>
          <button type="button" onClick={onCancel}>
            Cancel
          </button>
        </form>
      )}
    </section>
  )
}
