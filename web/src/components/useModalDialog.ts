import { useEffect, useEffectEvent, useRef } from 'react'

const FOCUSABLE = 'a[href], button:not(:disabled)'

/** Focus moves into the dialog on open, Tab stays inside it and Escape closes it. */
export function useModalDialog(onClose: () => void) {
  const dialog = useRef<HTMLDivElement>(null)
  const close = useEffectEvent(onClose)

  useEffect(() => {
    const root = dialog.current
    root?.querySelector<HTMLElement>(FOCUSABLE)?.focus()

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        close()
      } else if (event.key === 'Tab' && root) {
        keepFocusInside(root, event)
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [])

  return dialog
}

function keepFocusInside(root: HTMLElement, event: KeyboardEvent) {
  const items = Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE))
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
