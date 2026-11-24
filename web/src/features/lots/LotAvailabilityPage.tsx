import { type FormEvent, useState } from 'react'
import { Link, useParams } from 'react-router'
import {
  type Availability,
  AvailabilityError,
  formatRate,
  formatWindow,
  type Lot,
  LotNotFoundError,
  UNEXPECTED_ERROR,
  useAvailability,
  useLot,
} from './api'
import { addMinutes, type Interval, lotToday, quarterHours, STEP_MINUTES, toLotInstant } from './interval'
import { IntervalPicker } from './IntervalPicker'

export function LotAvailabilityPage() {
  const { id = '' } = useParams()
  const lot = useLot(id)

  if (lot.isPending) {
    return <p role="status">Loading lot…</p>
  }

  if (lot.isError) {
    return (
      <section>
        <p role="alert">
          {lot.error instanceof LotNotFoundError ? lot.error.message : 'We could not load the parking lot. Please try again.'}
        </p>
        <Link to="/lots">Back to all lots</Link>
      </section>
    )
  }

  // Keyed, so a different lot starts with its own default interval.
  return <AvailabilityView key={lot.data.id} lot={lot.data} />
}

function AvailabilityView({ lot }: { lot: Lot }) {
  const [value, setValue] = useState<Interval>(() => defaultInterval(lot))
  const [submitted, setSubmitted] = useState<{ start: string; end: string } | null>(null)
  const availability = useAvailability(lot.id, submitted)

  const errors = availability.error instanceof AvailabilityError ? availability.error.errors : undefined

  function submit(event: FormEvent) {
    event.preventDefault()
    setSubmitted({ start: toLotInstant(value.date, value.start), end: toLotInstant(value.date, value.end) })
  }

  function change(next: Interval) {
    setValue(next)
    // The old answer belongs to the old interval.
    setSubmitted(null)
  }

  return (
    <section>
      <h2>{lot.name}</h2>
      <p>
        {lot.address} · {formatRate(lot)} · Open {formatWindow(lot)}
      </p>

      <form onSubmit={submit} noValidate>
        {errors?.form && <p role="alert">{errors.form}</p>}
        <IntervalPicker
          opensAt={lot.opensAt}
          closesAt={lot.closesAt}
          value={value}
          onChange={change}
          errors={errors?.fields}
        />
        <button type="submit" disabled={availability.isFetching}>
          Check availability
        </button>
      </form>

      {availability.isFetching && <p role="status">Checking availability…</p>}
      {availability.isError && !errors && <p role="alert">{UNEXPECTED_ERROR}</p>}
      {availability.data && !availability.isFetching && <AvailabilityResult availability={availability.data} />}

      <Link to="/lots">Back to all lots</Link>
    </section>
  )
}

function AvailabilityResult({ availability }: { availability: Availability }) {
  if (availability.freeSpaces.length === 0) {
    return <p>No free spaces for this interval. Try another time.</p>
  }

  return (
    <section aria-label="Availability">
      <p>
        {availability.availableSpaces} of {availability.totalSpaces} spaces free
      </p>
      <ul>
        {availability.freeSpaces.map((space) => (
          <li key={space.id}>{space.label}</li>
        ))}
      </ul>
    </section>
  )
}

// The first hour after opening, or less if the lot closes sooner.
function defaultInterval(lot: Lot): Interval {
  const start = quarterHours(lot.opensAt, lot.closesAt)[0] ?? lot.opensAt.slice(0, 5)
  const ends = quarterHours(addMinutes(start, STEP_MINUTES), lot.closesAt)
  return { date: lotToday(), start, end: ends[Math.min(3, ends.length - 1)] ?? start }
}
