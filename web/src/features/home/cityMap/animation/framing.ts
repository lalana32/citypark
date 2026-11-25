import { MAP_HEIGHT, MAP_WIDTH, pointOf } from '../map/network'
import { PARKING_LOTS } from '../map/parking'
import { DRIVERS, HERO_MAP_SHIFT, HERO_STACK_BREAKPOINT, MARKER_RADIUS, MOTION_PADDING_PX } from './config'

/** Half-diagonal of a parking badge incl. its dark pad (map units), so badges are never clipped. */
const LOT_RADIUS = 34

interface Rect {
  x0: number
  y0: number
  x1: number
  y1: number
}

/**
 * The "safe area": everything that moves or pulses, i.e. every route node (rounded
 * corners only cut inside this box) expanded by the marker radius, plus every parking
 * badge. Derived from the config, so editing routes or lots updates it automatically.
 */
export const SAFE_AREA: Rect = (() => {
  const r: Rect = { x0: Infinity, y0: Infinity, x1: -Infinity, y1: -Infinity }
  const add = (x: number, y: number, pad: number) => {
    r.x0 = Math.min(r.x0, x - pad)
    r.y0 = Math.min(r.y0, y - pad)
    r.x1 = Math.max(r.x1, x + pad)
    r.y1 = Math.max(r.y1, y + pad)
  }
  for (const d of DRIVERS) for (const id of d.route) add(pointOf(id).x, pointOf(id).y, MARKER_RADIUS)
  for (const lot of PARKING_LOTS) add(lot.x, lot.y, LOT_RADIUS)
  return r
})()

const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v))

/**
 * Picks the visible part of the map for a frame of `width × height` pixels.
 *
 * 1. Start from "cover": the 1600 × 1000 map fills the frame, cropped evenly.
 * 2. If that crop would cut into the safe area (+ MOTION_PADDING_PX on screen),
 *    zoom out just enough for it to fit. The map extends far beyond 1600 × 1000,
 *    so zooming out never reveals an empty edge.
 * 3. Place the window for the hero layout, then slide it the minimum needed to keep
 *    the safe area inside it:
 *    - wide frames: map shifted right by HERO_MAP_SHIFT (headline sits on the left),
 *    - stacked (narrow) frames: drivers placed in the lower part, below the text.
 */
export function frameViewBox(width: number, height: number) {
  if (width <= 0 || height <= 0) return { x: 0, y: 0, w: MAP_WIDTH, h: MAP_HEIGHT }
  const safeW = SAFE_AREA.x1 - SAFE_AREA.x0
  const safeH = SAFE_AREA.y1 - SAFE_AREA.y0
  const pad = MOTION_PADDING_PX

  const cover = Math.max(width / MAP_WIDTH, height / MAP_HEIGHT)
  // Largest scale at which safe area + padding (in px) still fits: size_px = s·safe + 2·pad
  const fit = Math.min((width - 2 * pad) / safeW, (height - 2 * pad) / safeH)
  const scale = Math.max(0.01, Math.min(cover, fit))

  const w = width / scale
  const h = height / scale
  const padUnits = pad / scale
  const stacked = width < HERO_STACK_BREAKPOINT

  const idealX = MAP_WIDTH / 2 - w / 2 - (stacked ? 0 : HERO_MAP_SHIFT)
  const safeCy = (SAFE_AREA.y0 + SAFE_AREA.y1) / 2
  const idealY = stacked ? safeCy - h * 0.68 : MAP_HEIGHT / 2 - h / 2

  const x = clamp(idealX, SAFE_AREA.x1 + padUnits - w, SAFE_AREA.x0 - padUnits)
  const y = clamp(idealY, SAFE_AREA.y1 + padUnits - h, SAFE_AREA.y0 - padUnits)
  return { x, y, w, h }
}
