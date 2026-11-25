import { useEffect, useRef } from 'react'
import { Link } from 'react-router'
import { Logo } from '../../components/site/Logo'
import { withReturnTo } from './returnTo'
import './authPrompt.css'

type Props = {
  /** The in-app path to come back to after login or registration. */
  returnTo: string
  onClose: () => void
}

/** Modal shown to a guest who tries to reserve: sign-in is asked for only at this step. */
export function AuthPrompt({ returnTo, onClose }: Props) {
  const dialog = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const root = dialog.current
    root?.querySelector<HTMLElement>('a, button')?.focus()

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      } else if (event.key === 'Tab' && root) {
        keepFocusInside(root, event)
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div
      className="auth-overlay"
      onPointerDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div
        ref={dialog}
        className="auth-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="auth-title"
        aria-describedby="auth-text"
      >
        <Logo tone="light" />
        <h2 id="auth-title" className="site-display auth-title">
          Log in to continue
        </h2>
        <p id="auth-text" className="auth-text">
          To reserve a parking space, you need to log in to your account.
        </p>
        <div className="auth-actions">
          <Link className="site-btn site-btn-primary" to={withReturnTo('/login', returnTo)}>
            Log in
          </Link>
          <Link className="site-btn site-btn-secondary" to={withReturnTo('/register', returnTo)}>
            Register
          </Link>
          <button type="button" className="auth-dismiss" onClick={onClose}>
            Continue browsing
          </button>
        </div>
      </div>
    </div>
  )
}

function keepFocusInside(root: HTMLElement, event: KeyboardEvent) {
  const items = Array.from(root.querySelectorAll<HTMLElement>('a, button'))
  const first = items[0]
  const last = items[items.length - 1]

  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}
