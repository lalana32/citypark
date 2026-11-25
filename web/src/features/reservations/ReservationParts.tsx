import type { UseMutationResult } from '@tanstack/react-query'
import { useState } from 'react'
import { CancelRejectedError, type Reservation, UNEXPECTED_ERROR } from './api'

const STATE_LABELS: Record<Reservation['state'], string> = {
  Upcoming: 'Upcoming',
  InProgress: 'In progress',
  Finished: 'Finished',
  Cancelled: 'Cancelled',
}

export function StateBadge({ state }: { state: Reservation['state'] }) {
  return <span className={`badge badge-${state}`}>{STATE_LABELS[state]}</span>
}

type CancelProps = {
  reservationId: string
  cancellable: boolean
  // Each row owns its mutation, so one row's pending or error state never shows on another.
  cancel: UseMutationResult<void, Error, string>
}

export function CancelAction({ reservationId, cancellable, cancel }: CancelProps) {
  const [confirming, setConfirming] = useState(false)

  const error =
    cancel.error instanceof CancelRejectedError ? cancel.error.message : cancel.isError ? UNEXPECTED_ERROR : null

  const confirm = () => cancel.mutate(reservationId, { onSettled: () => setConfirming(false) })

  return (
    <>
      {cancellable && !confirming && (
        <button type="button" onClick={() => setConfirming(true)}>
          Cancel
        </button>
      )}
      {cancellable && confirming && (
        <div role="group" aria-label="Confirm cancellation">
          <span>Cancel this reservation?</span>
          <button type="button" disabled={cancel.isPending} onClick={confirm}>
            {cancel.isPending ? 'Cancelling…' : 'Yes, cancel'}
          </button>
          <button type="button" disabled={cancel.isPending} onClick={() => setConfirming(false)}>
            Keep it
          </button>
        </div>
      )}
      {error && <p role="alert">{error}</p>}
    </>
  )
}
