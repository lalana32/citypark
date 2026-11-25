import { type FormEvent, useCallback, useState } from 'react'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router'
import {
  AvailabilityError,
  type Lot,
  LotNotFoundError,
  UNEXPECTED_ERROR,
  useAvailability,
  useLot,
  useQuote,
} from './api'
import { addMinutes, type Interval, lotToday, quarterHours, readInterval, STEP_MINUTES, toLotInstant } from './interval'
import { IntervalPicker } from './IntervalPicker'
import { BACK_TO_LOTS, LotHeader } from './LotHeader'
import { type FreeSpace, SpaceGrid } from './SpaceGrid'
import { SummaryPanel } from './SummaryPanel'
import './lot.css'
import { SignedOutError, SPACE_TAKEN, SpaceTakenError, useCreateReservation } from '../reservations/api'
import { BookingPanel } from '../reservations/BookingPanel'
import { BookingSuccess } from '../reservations/BookingSuccess'
import { GuestBooking } from '../reservations/GuestBooking'
import { useSession } from '../auth/api'
import { AuthPrompt } from '../auth/AuthPrompt'
import { withReturnTo } from '../auth/returnTo'

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
  // The URL holds the checked interval and the chosen space, so a reload or a return keeps them.
  const [params, setParams] = useSearchParams()
  const checkedInUrl = readInterval(params, lot)
  const [value, setValue] = useState<Interval>(() => checkedInUrl ?? defaultInterval(lot))
  const submitted = checkedInUrl && {
    start: toLotInstant(checkedInUrl.date, checkedInUrl.start),
    end: toLotInstant(checkedInUrl.date, checkedInUrl.end),
  }
  const availability = useAvailability(lot.id, submitted)
  const spaceId = checkedInUrl ? params.get('space') : null
  const space = availability.data?.freeSpaces.find((free) => free.id === spaceId) ?? null
  const booking = useCreateReservation(lot.id)
  const navigate = useNavigate()
  const location = useLocation()
  const session = useSession()
  const [asking, setAsking] = useState(false)
  // Stable, so the dialog does not move focus again on every render.
  const closePrompt = useCallback(() => setAsking(false), [])

  const checked = availability.isSuccess ? checkedInUrl : null
  const quote = useQuote(lot.id, checked && submitted)
  const errors = availability.error instanceof AvailabilityError ? availability.error.errors : undefined

  function show(interval: Interval | null, nextSpaceId: string | null = null) {
    setParams(interval ? { ...interval, ...(nextSpaceId ? { space: nextSpaceId } : {}) } : {}, { replace: true })
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    booking.reset()
    show(value)
  }

  function choose(next: FreeSpace | null) {
    booking.reset()
    show(checkedInUrl, next?.id)
  }

  function book(plate: string) {
    if (!space || !submitted) {
      return
    }
    booking.mutate(
      { spaceId: space.id, ...submitted, plate },
      {
        // The space is booked now, so it no longer belongs in the URL.
        onSuccess: () => show(checkedInUrl),
        onError: (error) => {
          if (error instanceof SpaceTakenError) {
            // The hook has already refreshed the free spaces; this one is gone.
            show(checkedInUrl)
          } else if (error instanceof SignedOutError) {
            void navigate(withReturnTo('/login', location.pathname + location.search), { state: { sessionEnded: true } })
          }
        },
      },
    )
  }

  function change(next: Interval) {
    setValue(next)
    // The old answer belongs to the old interval.
    booking.reset()
    show(null)
  }

  return (
    <>
      <LotHeader lot={lot} />

      <div className="lot-layout">
        <div className="lot-main">
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

          {availability.isFetching && (
            <p role="status" className="lot-status">
              Checking availability…
            </p>
          )}
          {availability.isError && !errors && (
            <p role="alert" className="lot-alert lot-spaces">
              {UNEXPECTED_ERROR}
            </p>
          )}
          {booking.error instanceof SpaceTakenError && (
            <p role="alert" className="lot-alert lot-spaces">
              {SPACE_TAKEN}
            </p>
          )}
          {availability.data && !availability.isFetching && (
            <SpaceGrid availability={availability.data} selectedId={space?.id ?? null} onSelect={choose} />
          )}
        </div>

        <SummaryPanel
          lotName={lot.name}
          checked={checked}
          spaceLabel={booking.data?.spaceLabel ?? space?.label ?? null}
          quote={quote}
        >
          {booking.data ? (
            <BookingSuccess reservation={booking.data} />
          ) : (
            space &&
            submitted &&
            !session.isPending &&
            (session.data === null ? (
              <GuestBooking spaceLabel={space.label} onReserve={() => setAsking(true)} />
            ) : (
              <BookingPanel
                key={space.id}
                spaceLabel={space.label}
                pending={booking.isPending}
                error={booking.error}
                onConfirm={book}
                onDismissError={() => booking.reset()}
                onCancel={() => choose(null)}
              />
            ))
          )}
        </SummaryPanel>
      </div>

      {asking && <AuthPrompt returnTo={location.pathname + location.search} onClose={closePrompt} />}
    </>
  )
}

// The first hour after opening, or less if the lot closes sooner.
function defaultInterval(lot: Lot): Interval {
  const start = quarterHours(lot.opensAt, lot.closesAt)[0] ?? lot.opensAt.slice(0, 5)
  const ends = quarterHours(addMinutes(start, STEP_MINUTES), lot.closesAt)
  return { date: lotToday(), start, end: ends[Math.min(3, ends.length - 1)] ?? start }
}
