import { type FormEvent, useState } from 'react'
import { BookingRuleError, SignedOutError, SpaceTakenError, UNEXPECTED_ERROR } from './api'

type Props = {
  spaceLabel: string
  pending: boolean
  error: Error | null
  onConfirm: (plate: string) => void
  onDismissError: () => void
  onCancel: () => void
}

/** The confirm step: the summary above it shows what is booked, this adds the plate. */
export function BookingPanel({ spaceLabel, pending, error, onConfirm, onDismissError, onCancel }: Props) {
  const [plate, setPlate] = useState('')

  const errors = error instanceof BookingRuleError ? error.errors : undefined
  // The page handles 409 and 401 itself.
  const unexpected = error !== null && !errors && !(error instanceof SpaceTakenError) && !(error instanceof SignedOutError)
  const alert = unexpected
    ? UNEXPECTED_ERROR
    : [errors?.form, errors?.fields.start, errors?.fields.end].filter(Boolean).join(' ')
  const plateError = errors?.fields.plate

  function edit(next: string) {
    setPlate(next)
    onDismissError()
  }

  function confirm(event: FormEvent) {
    event.preventDefault()
    // A second Enter while the request runs must not send it again.
    if (pending || plate.trim() === '') {
      return
    }
    onConfirm(plate.trim())
  }

  return (
    <section className="lot-booking" aria-label="Booking">
      <h3 className="lot-booking-title">Book space {spaceLabel}</h3>
      {alert && (
        <p role="alert" className="lot-alert">
          {alert}
        </p>
      )}

      <form onSubmit={confirm} noValidate>
        <fieldset className="lot-booking-fields" disabled={pending}>
          <div className="lot-field">
            <label htmlFor="booking-plate">License plate</label>
            <input
              id="booking-plate"
              className="lot-input"
              value={plate}
              autoComplete="off"
              onChange={(event) => edit(event.target.value)}
              aria-describedby={plateError ? 'booking-plate-hint booking-plate-error' : 'booking-plate-hint'}
              {...(plateError && { 'aria-invalid': true })}
            />
            <p id="booking-plate-hint" className="lot-field-hint">
              For example A12K345
            </p>
            {plateError && (
              <p id="booking-plate-error" className="lot-field-error">
                {plateError}
              </p>
            )}
          </div>

          <div className="lot-booking-actions">
            <button className="site-btn site-btn-primary" type="submit" disabled={plate.trim() === ''}>
              {pending ? 'Booking…' : 'Confirm booking'}
            </button>
            <button className="site-btn site-btn-secondary" type="button" onClick={onCancel}>
              Cancel
            </button>
          </div>
        </fieldset>
      </form>
    </section>
  )
}
