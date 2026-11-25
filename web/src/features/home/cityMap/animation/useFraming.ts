import { useLayoutEffect, type RefObject } from 'react'
import { frameViewBox } from './framing'

/**
 * Keeps the map layers' viewBox matched to their frame so that moving markers never touch
 * the edge (see framing.ts). Writes attributes directly — no React re-renders.
 */
export function useFraming(rootRef: RefObject<HTMLElement | null>) {
  useLayoutEffect(() => {
    const frame = rootRef.current
    if (!frame) return
    // Every map layer (static streets + animated markers) shares one viewBox.
    const layers = frame.querySelectorAll<SVGSVGElement>('svg.city-map-layer')

    const apply = () => {
      const { x, y, w, h } = frameViewBox(frame.clientWidth, frame.clientHeight)
      const viewBox = `${x.toFixed(2)} ${y.toFixed(2)} ${w.toFixed(2)} ${h.toFixed(2)}`
      layers.forEach((svg) => svg.setAttribute('viewBox', viewBox))
      frame.querySelectorAll('.viewport-fill').forEach((el) => {
        el.setAttribute('x', x.toFixed(2))
        el.setAttribute('y', y.toFixed(2))
        el.setAttribute('width', w.toFixed(2))
        el.setAttribute('height', h.toFixed(2))
      })
    }

    apply()
    if (typeof ResizeObserver === 'undefined') return // jsdom in tests
    const observer = new ResizeObserver(apply)
    observer.observe(frame)
    return () => observer.disconnect()
  }, [rootRef])
}
