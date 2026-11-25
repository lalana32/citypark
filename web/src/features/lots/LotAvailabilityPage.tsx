import { type FormEvent, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import {
  AvailabilityError,
  type Lot,
  LotNotFoundError,
  UNEXPECTED_ERROR,
  useAvailability,
  useLot,
} from './api'
import { addMinutes, type Interval, lotToday, quarterHours, STEP_MINUTES, toLotInstant } from './interval'
import { IntervalPicker } from './IntervalPicker'
import { BACK_TO_LOTS, LotHeader } from './LotHeader'
import { type FreeSpace, SpaceGrid } from './SpaceGrid'
import './lot.css'
import { SignedOutError, SPACE_TAKEN, SpaceTakenError, useCreateReservation } from '../reservations/api'
import { BookingPanel } from '../reservations/BookingPanel'
import { BookingSuccess } from '../reservations/BookingSuccess'

export function LotAvailabilityPage() {
  const { id = '' } = useParams()
  const lot = useLot(id)

  return (
    <section className="lot-page">
      <div className="site-wrap">
        {lot.isPending && (
          <p role="status" className="lot-status">
            Loading lot…
          </p>
        )}
        {lot.isError && (
          <>
            <p role="alert" className="lot-alert">
              {lot.error instanceof LotNotFoundError
                ? lot.error.message
                : 'We could not load the parking lot. Please try again.'}
            </p>
            <Link className="lot-back" to={BACK_TO_LOTS}>
              ← Back to parking lots
            </Link>
          </>
        )}
        {/* Keyed, so a different lot starts with its own default interval. */}
        {lot.data && <AvailabilityView key={lot.data.id} lot={lot.data} />}
      </div>
    </section>
  )
}

function AvailabilityView({ lot }: { lot: Lot }) {
  const [value, setValue] = useState<Interval>(() => defaultInterval(lot))
  const [submitted, setSubmitted] = useState<{ start: string; end: string } | null>(null)
  const availability = useAvailability(lot.id, submitted)
  const [space, setSpace] = useState<FreeSpace | null>(null)
  const booking = useCreateReservation(lot.id)
  const navigate = useNavigate()
  const location = useLocation()

  const errors = availability.error instanceof AvailabilityError ? availability.error.errors : undefined

  function submit(event: FormEvent) {
    event.preventDefault()
    setSubmitted({ start: toLotInstant(value.date, value.start), end: toLotInstant(value.date, value.end) })
    choose(null)
  }

  function choose(next: FreeSpace | null) {
    booking.reset()
    setSpace(next)
  }

  function book(plate: string) {
    if (!space || !submitted) {
      return
    }
    booking.mutate(
      { spaceId: space.id, ...submitted, plate },
      {
        onError: (error) => {
          if (error instanceof SpaceTakenError) {
            // The hook has already refreshed the free spaces; this one is gone.
            setSpace(null)
          } else if (error instanceof SignedOutError) {
            void navigate('/login', { state: { from: location } })
          }
        },
      },
    )
  }

  function change(next: Interval) {
    setValue(next)
    // The old answer belongs to the old interval.
    setSubmitted(null)
    choose(null)
  }

  return (
    <>
      <LotHeader lot={lot} />

      <form className="site-card lot-form" onSubmit={submit} noValidate>
        {errors?.form && (
          <p role="alert" className="lot-alert">
            {errors.form}
          </p>
        )}
        <IntervalPicker
          opensAt={lot.opensAt}
          closesAt={lot.closesAt}
          value={value}
          onChange={change}
          errors={errors?.fields}
        />
        <div className="lot-form-actions">
          <button className="site-btn site-btn-primary" type="submit" disabled={availability.isFetching}>
            Check availability
          </button>
        </div>
      </form>

      {availability.isFetching && <p role="status">Checking availability…</p>}
      {availability.isError && !errors && <p role="alert">{UNEXPECTED_ERROR}</p>}
      {booking.error instanceof SpaceTakenError && <p role="alert">{SPACE_TAKEN}</p>}
      {availability.data && !availability.isFetching && (
        <SpaceGrid availability={availability.data} selectedId={space?.id ?? null} onSelect={choose} />
      )}

      {booking.data ? (
        <BookingSuccess reservation={booking.data} />
      ) : (
        space &&
        submitted && (
          <BookingPanel
            key={space.id}
            lotName={lot.name}
            spaceLabel={space.label}
            interval={submitted}
            pending={booking.isPending}
            error={booking.error}
            onConfirm={book}
            onDismissError={() => booking.reset()}
            onCancel={() => choose(null)}
          />
        )
      )}
    </>
  )
}

// The first hour after opening, or less if the lot closes sooner.
function defaultInterval(lot: Lot): Interval {
  const start = quarterHours(lot.opensAt, lot.closesAt)[0] ?? lot.opensAt.slice(0, 5)
  const ends = quarterHours(addMinutes(start, STEP_MINUTES), lot.closesAt)
  return { date: lotToday(), start, end: ends[Math.min(3, ends.length - 1)] ?? start }
}
