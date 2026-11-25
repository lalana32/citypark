import { useLayoutEffect, useRef, type RefObject } from 'react'
import gsap from 'gsap'
import { MotionPathPlugin } from 'gsap/MotionPathPlugin'
import {
  CYCLE_DURATION,
  DEFAULT_SPEED,
  PARKING_CHECK_DISTANCE,
  PARKING_PULSE_DURATION,
  TRAIL_LAYERS,
} from './config'
import { DRIVER_ROUTES } from './routes'
import { PARKING_LOTS } from '../map/parking'
import { THEME } from '../map/theme'

gsap.registerPlugin(MotionPathPlugin)

export interface AnimationController {
  play(): void
  pause(): void
  restart(): void
  setSpeed(speed: number): void
}

/** Point in the loop (0–1) shown when the user prefers reduced motion. */
const REDUCED_MOTION_FRAME = 0.18

/**
 * Builds one master timeline of exactly CYCLE_DURATION seconds and repeats it forever.
 *
 * Seamless-loop guarantees:
 *  - every driver completes exactly one lap of a closed path per cycle,
 *  - drivers are staggered by *where* they start on their loop (MotionPath start/end),
 *    never by delays, so t = 0 and t = CYCLE_DURATION are the same frame,
 *  - all tweens use `ease: "none"` for constant speed,
 *  - parking pulses that straddle the loop boundary are split across it.
 */
export function useCityAnimation(
  /** Container holding both map layers (markers + parking lots are looked up inside it). */
  rootRef: RefObject<HTMLElement | null>,
  onPlayStateChange?: (playing: boolean) => void,
) {
  const controller = useRef<AnimationController | null>(null)
  const onPlayStateRef = useRef(onPlayStateChange)
  useLayoutEffect(() => {
    onPlayStateRef.current = onPlayStateChange
  }, [onPlayStateChange])

  useLayoutEffect(() => {
    const root = rootRef.current
    if (!root) return
    // Environments without SVG geometry (jsdom in tests) get the static map only.
    const probe = root.querySelector<SVGPathElement>('.route-guide')
    if (!probe || typeof probe.getTotalLength !== 'function' || typeof window.matchMedia !== 'function') return
    const D = CYCLE_DURATION

    // gsap.context collects every tween so a single revert() cleans up (React Strict Mode safe).
    const ctx = gsap.context(() => {
      const master = gsap.timeline({ repeat: -1, paused: true, defaults: { ease: 'none' } })

      for (const route of DRIVER_ROUTES) {
        const group = root.querySelector<SVGGElement>(`[data-driver="${route.id}"]`)
        if (!group) continue
        const guide = group.querySelector<SVGPathElement>('.route-guide')!
        const marker = group.querySelector<SVGGElement>('.marker')!
        const trails = Array.from(group.querySelectorAll<SVGPathElement>('.trail'))
        const length = guide.getTotalLength()
        const s = route.offset

        // Marker: one lap, starting at its offset and wrapping around the closed path.
        master.to(
          marker,
          {
            duration: D,
            motionPath: { path: guide, start: s, end: s + 1 },
            immediateRender: true,
          },
          0,
        )

        // Trails: a single dash of `len` that ends exactly at the marker.
        // dash covers [p·L − len, p·L]  ⇒  dashoffset = len − p·L.
        trails.forEach((trail, i) => {
          const len = TRAIL_LAYERS[i][0]
          gsap.set(trail, { strokeDasharray: `${len} ${length - len}` })
          master.fromTo(
            trail,
            { strokeDashoffset: len - s * length },
            { strokeDashoffset: len - (s + 1) * length, duration: D, immediateRender: true },
            0,
          )
        })
        gsap.set([marker, ...trails], { visibility: 'visible' })

        // Parking checks: find where this route passes each lot and pulse it at that moment.
        const samples = samplePath(guide)
        for (const lot of PARKING_LOTS) {
          const passAt = closestProgress(samples, lot.x, lot.y)
          if (passAt.distance > PARKING_CHECK_DISTANCE) continue
          const t = (((passAt.progress - s) % 1) + 1) % 1 * D
          const el = root.querySelector(`[data-lot="${lot.id}"]`)
          if (el) addWrapped(master, lotPulse(el), Math.max(0, t - 0.15), D)
        }
      }

      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)')
      master.timeScale(DEFAULT_SPEED)

      const setPlaying = (playing: boolean) => {
        if (playing) master.play()
        else master.pause()
        onPlayStateRef.current?.(playing)
      }

      const applyMotionPreference = () => {
        if (reduce.matches) {
          master.progress(REDUCED_MOTION_FRAME)
          setPlaying(false)
        } else setPlaying(true)
      }
      applyMotionPreference()
      reduce.addEventListener('change', applyMotionPreference)

      // Everything is positioned and moving: reveal the map and hide the loader (CSS handles
      // the cross-fade). Set on the DOM directly so React never re-renders for it.
      root.setAttribute('data-ready', 'true')

      controller.current = {
        play: () => setPlaying(true),
        pause: () => setPlaying(false),
        restart: () => {
          master.restart()
          onPlayStateRef.current?.(true)
        },
        setSpeed: (speed) => {
          master.timeScale(speed)
        },
      }

      return () => reduce.removeEventListener('change', applyMotionPreference)
    }, root)

    return () => {
      ctx.revert()
      controller.current = null
    }
  }, [rootRef])

  return controller
}

/**
 * Paused timeline for one "this lot is full" pulse.
 * Progress 0 and 1 are both the resting state, so it can be scrubbed and split freely.
 */
function lotPulse(lot: Element) {
  const ring = lot.querySelector('.lot-ring')
  const badge = lot.querySelector('.lot-badge')
  const { stroke, full } = THEME.parking
  const P = PARKING_PULSE_DURATION
  return gsap
    .timeline({ paused: true, defaults: { ease: 'none' } })
    .fromTo(ring, { scale: 0.7, transformOrigin: '50% 50%' }, { scale: 2.2, duration: P, ease: 'power2.out' }, 0)
    .fromTo(ring, { opacity: 0 }, { opacity: 0.8, duration: P * 0.1 }, 0)
    .to(ring, { opacity: 0, duration: P * 0.9, ease: 'power1.in' }, P * 0.1)
    .fromTo(badge, { attr: { stroke } }, { attr: { stroke: full }, duration: P * 0.15 }, 0)
    .to(badge, { attr: { stroke }, duration: P * 0.6 }, P * 0.3)
}

/**
 * Plays `child` (a paused timeline) at time `t` inside a master of length D.
 * If it would overrun the loop boundary, the remainder plays from t = 0 so the
 * cycle length stays exactly D and nothing pops at the seam.
 */
function addWrapped(master: gsap.core.Timeline, child: gsap.core.Timeline, t: number, D: number) {
  const P = child.duration()
  const head = Math.min(P, D - t)
  master.add(child.tweenFromTo(0, head, { ease: 'none', immediateRender: false }), t)
  if (head < P) master.add(child.tweenFromTo(head, P, { ease: 'none', immediateRender: false }), 0)
}

const PATH_SAMPLES = 600

/**
 * Evenly spaced points along a route, computed once per route.
 * Uses MotionPathPlugin's pure-JS path maths (the same mapping that moves the markers)
 * instead of SVGPathElement.getPointAtLength, which is very slow — thousands of those
 * calls used to delay the start of the animation by several seconds.
 */
function samplePath(path: SVGPathElement) {
  const raw = MotionPathPlugin.cacheRawPathMeasurements(MotionPathPlugin.getRawPath(path))
  return Array.from({ length: PATH_SAMPLES }, (_, i) => {
    const p = MotionPathPlugin.getPositionOnPath(raw, i / PATH_SAMPLES)
    return { x: p.x, y: p.y }
  })
}

/** Progress (0–1) along the sampled path where it passes closest to (x, y). */
function closestProgress(samples: { x: number; y: number }[], x: number, y: number) {
  let best = { progress: 0, distance: Infinity }
  samples.forEach((p, i) => {
    const dist = Math.hypot(p.x - x, p.y - y)
    if (dist < best.distance) best = { progress: i / samples.length, distance: dist }
  })
  return best
}
