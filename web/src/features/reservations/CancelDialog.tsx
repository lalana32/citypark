import { useModalDialog } from '../../components/useModalDialog'
import { formatLotDay, formatLotTimes } from '../lots/interval'
import type { Reservation } from './api'

type Props = {
  reservation: Reservation
  pending: boolean
  onConfirm: () => void
  onClose: () => void
}

export function CancelDialog({ reservation, pending, onConfirm, onClose }: Props) {
  // While the request runs the dialog stays open, so the user sees it finish.
  const close = () => {
    if (!pending) {
      onClose()
    }
  }
  const dialog = useModalDialog(close)

  return (
    <div
      className="res-overlay"
      onPointerDown={(event) => {
        if (event.target === event.currentTarget) {
          close()
        }
      }}
    >
      <div
        ref={dialog}
        className="res-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="cancel-title"
        aria-describedby="cancel-text"
      >
        <h2 id="cancel-title" className="site-display res-dialog-title">
          Cancel this reservation?
        </h2>
        <div id="cancel-text" className="res-dialog-text">
          <p>
            Space {reservation.spaceLabel} at {reservation.lotName}, {formatLotDay(reservation.start)},{' '}
            {formatLotTimes(reservation.start, reservation.end)}.
          </p>
          <p>The space is released for others and this cannot be undone.</p>
        </div>
        <div className="res-dialog-actions">
          {/* The safe choice comes first, so it gets the focus. */}
          <button type="button" className="site-btn site-btn-secondary" disabled={pending} onClick={close}>
            Keep it
          </button>
          <button type="button" className="site-btn res-btn-danger" disabled={pending} onClick={onConfirm}>
            {pending ? 'Cancelling…' : 'Yes, cancel'}
          </button>
        </div>
      </div>
    </div>
  )
}
