import { useEffect, useId, useRef, useState } from 'react'
import { Link } from 'react-router'
import { Pager } from '../../components/Pager'
import {
  CancelRejectedError,
  canCancel,
  groupReservations,
  type Reservation,
  SignedOutError,
  UNEXPECTED_ERROR,
  useCancelReservation,
  useMyReservations,
} from './api'
import { CancelDialog } from './CancelDialog'
import { ReservationCard } from './ReservationCard'
import './reservations.css'

export const CANCELLED_NOTICE = 'Reservation cancelled. You can find it under Past.'

type Outcome = { kind: 'cancelled' } | { kind: 'failed'; message: string }

export function MyReservationsPage() {
  const [page, setPage] = useState(1)
  const reservations = useMyReservations(page)
  const now = useNow()
  const items = reservations.data?.items ?? []
  const groups = groupReservations(items)

  const cancel = useCancelReservation()
  const [toCancel, setToCancel] = useState<Reservation | null>(null)
  const [outcome, setOutcome] = useState<Outcome | null>(null)
  const opener = useRef<HTMLElement | null>(null)
  const outcomeRef = useRef<HTMLParagraphElement>(null)

  // The cancelled card moves to Past, so focus goes to the message instead of a button that is gone.
  useEffect(() => {
    if (outcome) {
      outcomeRef.current?.focus()
    }
  }, [outcome])

  const openDialog = (reservation: Reservation, button: HTMLElement) => {
    opener.current = button
    setOutcome(null)
    setToCancel(reservation)
  }

  const keep = () => {
    setToCancel(null)
    opener.current?.focus()
  }

  const confirm = (reservation: Reservation) =>
    cancel.mutate(reservation.id, {
      onSuccess: () => setOutcome({ kind: 'cancelled' }),
      onError: (error) => {
        // A signed-out user is sent to the login page, so there is nothing to say here.
        if (!(error instanceof SignedOutError)) {
          setOutcome({ kind: 'failed', message: error instanceof CancelRejectedError ? error.message : UNEXPECTED_ERROR })
        }
      },
      onSettled: () => setToCancel(null),
    })

  const changePage = (next: number) => {
    setOutcome(null)
    setPage(next)
  }

  return (
    <section className="res-page">
      <div className="site-wrap">
        <header className="res-header">
          <p className="site-eyebrow">Your bookings</p>
          <h1 className="site-h2 site-display">My reservations</h1>
          <p className="site-sub">Times are local time in Sarajevo.</p>
        </header>

        {outcome && (
          <p
            ref={outcomeRef}
            tabIndex={-1}
            role={outcome.kind === 'cancelled' ? 'status' : 'alert'}
            className={`res-outcome res-outcome-${outcome.kind}`}
          >
            {outcome.kind === 'cancelled' ? CANCELLED_NOTICE : outcome.message}
          </p>
        )}

        {reservations.isPending && <p role="status">Loading reservations…</p>}
        {reservations.isError && <p role="alert">We could not load your reservations. Please try again.</p>}
        {reservations.data && items.length === 0 && (
          <p>
            You have no reservations yet. <Link to="/lots">Find a parking lot</Link>
          </p>
        )}
        {reservations.data && items.length > 0 && (
          <>
            <ReservationGroup title="Upcoming" items={groups.upcoming} now={now} onCancel={openDialog} />
            <ReservationGroup title="Past" items={groups.past} now={now} onCancel={openDialog} />
            <Pager page={page} totalPages={Number(reservations.data.totalPages ?? 1)} onChange={changePage} />
          </>
        )}
      </div>

      {toCancel && (
        <CancelDialog
          reservation={toCancel}
          pending={cancel.isPending}
          onConfirm={() => confirm(toCancel)}
          onClose={keep}
        />
      )}
    </section>
  )
}

type GroupProps = {
  title: string
  items: Reservation[]
  now: number
  onCancel: (reservation: Reservation, button: HTMLElement) => void
}

function ReservationGroup({ title, items, now, onCancel }: GroupProps) {
  const titleId = useId()

  if (items.length === 0) {
    return null
  }

  return (
    <section className="res-group" aria-labelledby={titleId}>
      <h2 id={titleId} className="site-display res-group-title">
        {title}
      </h2>
      <ul className="res-list">
        {items.map((reservation) => (
          <li key={reservation.id}>
            <ReservationCard
              reservation={reservation}
              actions={
                canCancel(reservation, now) && (
                  <button
                    type="button"
                    className="site-btn site-btn-secondary site-btn-small"
                    onClick={(event) => onCancel(reservation, event.currentTarget)}
                  >
                    Cancel
                  </button>
                )
              }
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

/** The current time, refreshed every 30 seconds so the cancel button disappears at the cutoff. */
function useNow(intervalMs = 30_000): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(timer)
  }, [intervalMs])

  return now
}
